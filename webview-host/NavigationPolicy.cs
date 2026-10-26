using System;

namespace DeepSeekFloat
{
    // Main-frame allowlist for the pinned chat. Foreign https opens beside it;
    // the popup keeps its own visible host and is not limited to this list.
    internal static class NavigationPolicy
    {
        internal static bool AllowMainFrame(Uri uri)
        {
            if (uri==null || !uri.IsAbsoluteUri) return false;
            if (string.Equals(uri.Scheme,"about",StringComparison.OrdinalIgnoreCase))
                return string.Equals(uri.AbsolutePath,"blank",StringComparison.OrdinalIgnoreCase);
            if (!string.Equals(uri.Scheme,"https",StringComparison.OrdinalIgnoreCase)) return false;
            return IsDeepSeekHost(HostOf(uri));
        }
        internal static bool ShowHostChip(Uri uri)
        {
            if (uri==null || !uri.IsAbsoluteUri || !string.Equals(uri.Scheme,"https",StringComparison.OrdinalIgnoreCase)) return false;
            return !string.Equals(HostOf(uri),"chat.deepseek.com",StringComparison.OrdinalIgnoreCase);
        }
        internal static string DisplayHost(Uri uri)
        {
            string host=HostOf(uri);
            return string.IsNullOrEmpty(host) ? uri.Host : host;
        }
        private static string HostOf(Uri uri)
        {
            try { return uri.IdnHost ?? ""; }
            catch (Exception) { return uri.Host ?? ""; }
        }
        // ASCII / punycode only. A lookalike label does not end with .deepseek.com.
        private static bool IsDeepSeekHost(string host)
        {
            if (string.IsNullOrEmpty(host)) return false;
            host=host.Trim().TrimEnd('.').ToLowerInvariant();
            if (host.Length==0 || host.IndexOf(' ')>=0) return false;
            if (host=="deepseek.com") return true;
            if (!host.EndsWith(".deepseek.com",StringComparison.Ordinal)) return false;
            return host.IndexOf("..",StringComparison.Ordinal)<0 && !host.StartsWith(".",StringComparison.Ordinal);
        }
    }
}
