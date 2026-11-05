using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading;

namespace RocketLauncher
{
    /// <summary>
    /// Finds the game client for a year and starts it.
    /// Downloaded clients live in %LOCALAPPDATA%\Rocket\Versions\Client{year}\ and come from
    ///   {BaseUrl}/setup/Client{year}.version
    ///   {BaseUrl}/setup/Client{year}.zip
    /// </summary>
    public sealed class ClientManager
    {
        public const string ClientExeName = "RocketPlayerBeta.exe";
        const string VersionFileName = "rocket-version.txt";

        readonly LauncherConfig config;
        readonly string versionsDirectory;

        /// <summary>status text, progress 0-100 (or -1 when unknown)</summary>
        public Action<string, int> Progress = delegate { };

        public ClientManager(LauncherConfig config, string installDirectory)
        {
            this.config = config;
            versionsDirectory = Path.Combine(installDirectory, "Versions");
        }

        /// <summary>Returns the folder that holds a ready-to-run client for this year.</summary>
        public string EnsureClient(string year)
        {
            string local = config.GetLocalClientPath(year);
            if (local != null)
            {
                if (!File.Exists(Path.Combine(local, ClientExeName)))
                    throw new LauncherException("Client" + year + "Path in " + LauncherConfig.FileName +
                                                " points at a folder without " + ClientExeName + ":\n" + local);
                return local;
            }

            string clientDir = Path.Combine(versionsDirectory, "Client" + year);
            string installedVersion = ReadInstalledVersion(clientDir);

            Progress("Checking for updates...", -1);
            string latestVersion = null;
            try
            {
                latestVersion = DownloadString(config.BaseUrl + "/setup/Client" + year + ".version").Trim();
            }
            catch (WebException e)
            {
                // offline or site hiccup: still let them play if a client is already installed
                if (installedVersion != null) return clientDir;
                if (IsNotFound(e))
                    throw new LauncherException("The " + year + " client isn't available to download yet.", e);
                throw new LauncherException("Couldn't reach " + config.BaseUrl + " to download the game.\n\n" + e.Message, e);
            }

            if (installedVersion != null && installedVersion == latestVersion)
                return clientDir;

            InstallClient(year, latestVersion, clientDir);
            return clientDir;
        }

        void InstallClient(string year, string version, string clientDir)
        {
            Directory.CreateDirectory(versionsDirectory);
            string zipPath = Path.Combine(versionsDirectory, "Client" + year + ".download");
            string newDir = clientDir + ".new";

            try
            {
                Progress(installedOrUpdate(clientDir) + " Rocket (" + year + ")...", 0);
                DownloadFile(config.BaseUrl + "/setup/Client" + year + ".zip?v=" + Uri.EscapeDataString(version), zipPath);

                Progress("Installing Rocket (" + year + ")...", -1);
                if (Directory.Exists(newDir)) Directory.Delete(newDir, true);
                ExtractZip(zipPath, newDir);
                if (!File.Exists(Path.Combine(newDir, ClientExeName)))
                    throw new LauncherException("The downloaded " + year + " client is missing " + ClientExeName + ".");
                File.WriteAllText(Path.Combine(newDir, VersionFileName), version);

                if (Directory.Exists(clientDir)) Directory.Delete(clientDir, true);
                Directory.Move(newDir, clientDir);
            }
            catch (IOException e)
            {
                throw new LauncherException("Couldn't install the game. If Rocket is already open, close it and press Play again.\n\n" + e.Message, e);
            }
            catch (UnauthorizedAccessException e)
            {
                throw new LauncherException("Couldn't install the game. If Rocket is already open, close it and press Play again.\n\n" + e.Message, e);
            }
            finally
            {
                TryDelete(zipPath);
                try { if (Directory.Exists(newDir)) Directory.Delete(newDir, true); } catch { }
            }
        }

        static string installedOrUpdate(string clientDir)
        {
            return Directory.Exists(clientDir) ? "Updating" : "Downloading";
        }

        /// <summary>Starts the client and waits (up to ~20s) for its window, so the splash can close at the right time.</summary>
        public Process StartClient(string clientDir, string arguments)
        {
            var info = new ProcessStartInfo(Path.Combine(clientDir, ClientExeName), arguments)
            {
                WorkingDirectory = clientDir,
                UseShellExecute = false,
            };
            Process process = Process.Start(info);
            for (int i = 0; i < 80 && process != null && !process.HasExited; i++)
            {
                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero) break;
                Thread.Sleep(250);
            }
            return process;
        }

        static string ReadInstalledVersion(string clientDir)
        {
            string versionFile = Path.Combine(clientDir, VersionFileName);
            if (!File.Exists(versionFile) || !File.Exists(Path.Combine(clientDir, ClientExeName))) return null;
            return File.ReadAllText(versionFile).Trim();
        }

        static WebClient NewWebClient()
        {
            var web = new WebClient();
            web.Headers[HttpRequestHeader.UserAgent] = "RocketLauncher/1.0";
            return web;
        }

        static string DownloadString(string url)
        {
            using (var web = NewWebClient())
                return web.DownloadString(url);
        }

        void DownloadFile(string url, string path)
        {
            using (var web = NewWebClient())
            using (var done = new ManualResetEvent(false))
            {
                Exception error = null;
                web.DownloadProgressChanged += (s, e) =>
                {
                    int pct = e.TotalBytesToReceive > 0 ? (int)(e.BytesReceived * 100 / e.TotalBytesToReceive) : -1;
                    Progress(null, pct);
                };
                web.DownloadFileCompleted += (s, e) =>
                {
                    error = e.Error;
                    done.Set();
                };
                web.DownloadFileAsync(new Uri(url), path);
                done.WaitOne();
                if (error != null)
                    throw new LauncherException("Downloading the game failed.\n\n" + error.Message, error);
            }
        }

        /// <summary>Extracts a zip, refusing any entry that would land outside the target folder.</summary>
        public static void ExtractZip(string zipPath, string targetDir)
        {
            string root = Path.GetFullPath(targetDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(root);
            using (var stream = File.OpenRead(zipPath))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (var entry in zip.Entries)
                {
                    string relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                    string dest = Path.GetFullPath(Path.Combine(root, relative));
                    if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new LauncherException("The downloaded client has a bad file path: " + entry.FullName);
                    if (relative.EndsWith(Path.DirectorySeparatorChar.ToString()) || entry.Name.Length == 0)
                    {
                        Directory.CreateDirectory(dest);
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    using (var input = entry.Open())
                    using (var output = File.Create(dest))
                        input.CopyTo(output);
                }
            }
        }

        static bool IsNotFound(WebException e)
        {
            var response = e.Response as HttpWebResponse;
            return response != null && response.StatusCode == HttpStatusCode.NotFound;
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
