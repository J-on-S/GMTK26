using System.Text.RegularExpressions;

namespace BuildTools
{
    /// <summary>
    /// Parses an itch.io game page URL into its user (subdomain) and game slug.
    /// Accepts, tolerantly:
    ///   https://team7.itch.io/fish-game
    ///   team7.itch.io/fish-game
    ///   https://team7.itch.io/fish-game/   (trailing slash / query / fragment ignored)
    /// </summary>
    public static class ItchUrl
    {
        private static readonly Regex Pattern = new Regex(
            @"^(?:https?://)?(?<user>[^./\s]+)\.itch\.io/(?<game>[^/?#\s]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
