using System;
using System.IO;
using Microsoft.Win32;

namespace RocketLauncher
{
    /// <summary>
    /// Installs the launcher to %LOCALAPPDATA%\Rocket
    /// Windows user (no admin needed)
    /// </summary>
    public static class Installer
    {
        public static string InstallDirectory
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rocket"); }
        }

        public static string InstalledExePath
        {
            get { return Path.Combine(InstallDirectory, "RocketLauncher.exe"); }
        }

        const string ProtocolKey = @"Software\Classes\" + LaunchRequest.Scheme;

        /// <summary>Copies the running exe (and its ini, if any) into the install folder and registers the link.</summary>
        public static void Install(string runningExePath)
        {
            Directory.CreateDirectory(InstallDirectory);
            string runningDir = Path.GetDirectoryName(Path.GetFullPath(runningExePath));

            if (!PathsEqual(runningDir, InstallDirectory))
            {
                File.Copy(runningExePath, InstalledExePath, true);
                string ini = Path.Combine(runningDir, LauncherConfig.FileName);
                if (File.Exists(ini))
                    File.Copy(ini, Path.Combine(InstallDirectory, LauncherConfig.FileName), true);
            }

            RegisterProtocol(InstalledExePath);
        }

        public static void RegisterProtocol(string exePath)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(ProtocolKey))
            {
                key.SetValue("", "URL:Rocket Player Protocol");
                key.SetValue("URL Protocol", "");
                using (var icon = key.CreateSubKey("DefaultIcon"))
                    icon.SetValue("", "\"" + exePath + "\",0");
                using (var command = key.CreateSubKey(@"shell\open\command"))
                    command.SetValue("", "\"" + exePath + "\" \"%1\"");
            }
        }

        public static void Uninstall()
        {
            Registry.CurrentUser.DeleteSubKeyTree(ProtocolKey, false);
        }

        public static bool PathsEqual(string a, string b)
        {
            return string.Equals(
                Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
