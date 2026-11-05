using System;
using System.Collections.Generic;
using System.IO;

namespace RocketLauncher
{
    /// <summary>
    /// Settings from RocketLauncher.ini
    ///   BaseUrl=https://rockrev.de
    ///   Client2020Path=C:\path\to\Client2020
    ///   2016 bugged af still working on fixing it
    /// </summary>
    public sealed class LauncherConfig
    {
        public const string FileName = "RocketLauncher.ini";
        public const string DefaultBaseUrl = "https://rockrev.de";

        public string BaseUrl { get; private set; }
        readonly Dictionary<string, string> values;

        LauncherConfig(Dictionary<string, string> values)
        {
            this.values = values;
            string baseUrl;
            BaseUrl = values.TryGetValue("BaseUrl", out baseUrl) && IsHttpUrl(baseUrl)
                ? baseUrl.TrimEnd('/')
                : DefaultBaseUrl;
        }

        public static LauncherConfig Load(string directory)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string path = Path.Combine(directory, FileName);
            if (File.Exists(path))
            {
                foreach (var raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#") || line.StartsWith("[")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            return new LauncherConfig(values);
        }

        /// <summary>A local client folder set in the ini for this year, or null to use the downloaded client.</summary>
        public string GetLocalClientPath(string year)
        {
            string path;
            if (values.TryGetValue("Client" + year + "Path", out path) && path.Length > 0)
                return Environment.ExpandEnvironmentVariables(path);
            return null;
        }

        static bool IsHttpUrl(string s)
        {
            Uri uri;
            return Uri.TryCreate(s, UriKind.Absolute, out uri) &&
                   (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
        }
    }
}
