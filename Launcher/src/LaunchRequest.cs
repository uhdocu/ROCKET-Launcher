using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace RocketLauncher
{
    /// <summary>
    /// A parsed and validated  rocket-player://join?place=123&amp;ticket=...&amp;year=2020  link.
    /// Any web page can open a rocket-player:// link, so only values that look exactly like what
    /// the Rocket website sends are accepted.
    /// </summary>
    public sealed class LaunchRequest
    {
        public const string Scheme = "rocket-player";

        public long PlaceId { get; private set; }
        public string Ticket { get; private set; }
        public string Year { get; private set; }

        static readonly Regex PlaceRegex = new Regex(@"^\d{1,19}$");
        static readonly Regex TicketRegex = new Regex(@"^[A-Za-z0-9._~+/=%-]{1,4096}$");
        static readonly Regex YearRegex = new Regex(@"^\d{4}$");

        public static bool IsLaunchLink(string arg)
        {
            return arg != null && arg.StartsWith(Scheme + ":", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Throws LauncherException with a message if the link is bad.</summary>
        public static LaunchRequest Parse(string link)
        {
            if (!IsLaunchLink(link))
                throw new LauncherException("This isn't a Rocket game link.");

            var query = ParseQuery(link);
            string place, ticket, year;
            query.TryGetValue("place", out place);
            query.TryGetValue("ticket", out ticket);
            query.TryGetValue("year", out year);
            if (string.IsNullOrEmpty(year)) year = "2020";

            if (place == null || !PlaceRegex.IsMatch(place))
                throw new LauncherException("This game link has a bad place id.");
            if (string.IsNullOrEmpty(ticket))
                throw new LauncherException("You need to be logged in on the website. Log in and press Play again.");
            if (!TicketRegex.IsMatch(ticket))
                throw new LauncherException("This game link has a bad login ticket. Press Play again.");
            if (!YearRegex.IsMatch(year))
                throw new LauncherException("This game link has a bad client year.");

            long placeId;
            if (!long.TryParse(place, out placeId) || placeId <= 0)
                throw new LauncherException("This game link has a bad place id.");

            return new LaunchRequest { PlaceId = placeId, Ticket = ticket, Year = year };
        }

        static Dictionary<string, string> ParseQuery(string link)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int q = link.IndexOf('?');
            if (q < 0) return result;
            foreach (var pair in link.Substring(q + 1).Split('&'))
            {
                if (pair.Length == 0) continue;
                int eq = pair.IndexOf('=');
                string key = Uri.UnescapeDataString(eq < 0 ? pair : pair.Substring(0, eq));
                string value = eq < 0 ? "" : Uri.UnescapeDataString(pair.Substring(eq + 1));
                result[key] = value;
            }
            return result;
        }

        /// <summary>
        /// Command line for RocketPlayerBeta.exe - the same values the website's /game/get-join-script returns:
        /// -a Negotiate.ashx (login), -t ticket, -j PlaceLauncher.ashx
        /// </summary>
        public string BuildClientArguments(string baseUrl, long launchTimeMs)
        {
            string auth = baseUrl + "/Login/Negotiate.ashx";
            string placeLauncher = baseUrl + "/game/PlaceLauncher.ashx?request=RequestGame&placeid=" + PlaceId +
                                   "&ticket=" + Uri.EscapeDataString(Ticket) + "&" + Year + "=true";
            string args = "--play -a \"" + auth + "\" -t \"" + Ticket + "\" -j \"" + placeLauncher + "\" -b 0";
            if (Year != "2016")
            {
                args += " --launchtime=" + launchTimeMs + " --rloc en_us --gloc en_us";
            }
            return args;
        }
    }

    /// <summary>An error whose message can be shown straight to the player.</summary>
    public sealed class LauncherException : Exception
    {
        public LauncherException(string message) : base(message) { }
        public LauncherException(string message, Exception inner) : base(message, inner) { }
    }
}
