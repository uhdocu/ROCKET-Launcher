using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace RocketLauncher
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string exePath = Application.ExecutablePath;
            try
            {
                if (args.Any(a => a.Equals("-uninstall", StringComparison.OrdinalIgnoreCase)))
                {
                    Installer.Uninstall();
                    MessageBox.Show("The rocket-player:// link has been removed.", "Rocket", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }

                string link = args.FirstOrDefault(LaunchRequest.IsLaunchLink);
                if (link == null)
                {
                    Installer.Install(exePath);
                    MessageBox.Show("Rocket is installed!\n\nGo to the website and press Play on a game to join it.",
                        "Rocket", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }

                return Launch(LaunchRequest.Parse(link), exePath);
            }
            catch (Exception e)
            {
                ShowError(e);
                return 1;
            }
        }

        static int Launch(LaunchRequest request, string exePath)
        {
            // keep the link pointing at this launcher (repairs it if the folder was moved)
            try { Installer.RegisterProtocol(exePath); } catch { }

            var config = LauncherConfig.Load(Path.GetDirectoryName(exePath));
            var clients = new ClientManager(config, Installer.InstallDirectory);
            var splash = new SplashForm();
            clients.Progress = splash.SetProgress;

            Exception failure = null;
            splash.Shown += (s, e) =>
            {
                var worker = new Thread(() =>
                {
                    try
                    {
                        string clientDir = clients.EnsureClient(request.Year);
                        if (splash.UserCancelled) return;
                        splash.SetProgress("Starting Rocket...", -1);
                        long now = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                        clients.StartClient(clientDir, request.BuildClientArguments(config.BaseUrl, now));
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                    }
                    finally
                    {
                        splash.CloseFromAnyThread();
                    }
                });
                worker.IsBackground = true;
                worker.Start();
            };
            Application.Run(splash);

            if (failure != null && !splash.UserCancelled)
            {
                ShowError(failure);
                return 1;
            }
            return 0;
        }

        static void ShowError(Exception e)
        {
            string message = e is LauncherException
                ? e.Message
                : "Something went wrong starting Rocket:\n\n" + e.Message;
            MessageBox.Show(message, "Rocket", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
