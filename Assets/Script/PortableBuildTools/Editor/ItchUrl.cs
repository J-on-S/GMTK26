using System.Text.RegularExpressions;

namespace BuildTools
{
    /// <summary>Splits an itch.io game page URL into its user and game slug.</summary>
    public static class ItchUrl
    {
        private static readonly Regex Pattern = new Regex(
            @"^(?:https?://)?(?<user>[^./\s]+)\.itch\.io/(?<game>[^/?#\s]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Reads the user and game slug out of an itch.io page URL.</summary>
        /// <param name="url">The scheme, a trailing slash, a query, and a fragment are all optional
        /// — <c>team7.itch.io/fish-game</c> parses the same as the full <c>https://</c> form.</param>
        /// <returns><c>true</c> on a match; <c>false</c> leaves both outputs <c>null</c>.</returns>
        public static bool TryParse(string url, out string user, out string game)
        {
            user = null;
            game = null;
            if (string.IsNullOrWhiteSpace(url)) return false;

            Match m = Pattern.Match(url.Trim());
            if (!m.Success) return false;

            user = m.Groups["user"].Value;
            game = m.Groups["game"].Value;
            return true;
        }
    }
}
