using System.Text.RegularExpressions;

namespace DaisiBot.LocalTools.Browser
{
    /// <summary>
    /// Lightweight HTML-to-Markdown converter for browser extraction.
    /// </summary>
    internal static class HtmlConverter
    {
        internal static string Convert(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            var result = html;

            // Remove script, style, nav, footer elements entirely
            result = Regex.Replace(result, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"<style[^>]*>[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"<nav[^>]*>[\s\S]*?</nav>", "", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"<footer[^>]*>[\s\S]*?</footer>", "", RegexOptions.IgnoreCase);

            // Headings
            result = Regex.Replace(result, @"<h1[^>]*>(.*?)</h1>", "\n# $1\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            result = Regex.Replace(result, @"<h2[^>]*>(.*?)</h2>", "\n## $1\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            result = Regex.Replace(result, @"<h3[^>]*>(.*?)</h3>", "\n### $1\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            result = Regex.Replace(result, @"<h4[^>]*>(.*?)</h4>", "\n#### $1\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            result = Regex.Replace(result, @"<h5[^>]*>(.*?)</h5>", "\n##### $1\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            result = Regex.Replace(result, @"<h6[^>]*>(.*?)</h6>", "\n###### $1\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Links
            result = Regex.Replace(result, @"<a[^>]*href=""([^""]*)""[^>]*>(.*?)</a>", "[$2]($1)", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Bold / Strong
            result = Regex.Replace(result, @"<(?:strong|b)[^>]*>(.*?)</(?:strong|b)>", "**$1**", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Italic / Em
            result = Regex.Replace(result, @"<(?:em|i)[^>]*>(.*?)</(?:em|i)>", "*$1*", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Code (inline)
            result = Regex.Replace(result, @"<code[^>]*>(.*?)</code>", "`$1`", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Pre/code blocks
            result = Regex.Replace(result, @"<pre[^>]*><code[^>]*>(.*?)</code></pre>", "\n```\n$1\n```\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            result = Regex.Replace(result, @"<pre[^>]*>(.*?)</pre>", "\n```\n$1\n```\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Unordered list items
            result = Regex.Replace(result, @"<li[^>]*>(.*?)</li>", "- $1\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            result = Regex.Replace(result, @"</?[uo]l[^>]*>", "\n", RegexOptions.IgnoreCase);

            // Blockquote
            result = Regex.Replace(result, @"<blockquote[^>]*>(.*?)</blockquote>", "\n> $1\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Horizontal rule
            result = Regex.Replace(result, @"<hr[^>]*/?>", "\n---\n", RegexOptions.IgnoreCase);

            // Line breaks and paragraphs
            result = Regex.Replace(result, @"<br[^>]*/?>", "\n", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"<p[^>]*>(.*?)</p>", "\n$1\n", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Images
            result = Regex.Replace(result, @"<img[^>]*alt=""([^""]*)""[^>]*src=""([^""]*)""[^>]*/?>", "![$1]($2)", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"<img[^>]*src=""([^""]*)""[^>]*alt=""([^""]*)""[^>]*/?>", "![$2]($1)", RegexOptions.IgnoreCase);

            // Strip all remaining HTML tags
            result = Regex.Replace(result, @"<[^>]+>", "");

            // Decode common HTML entities
            result = result.Replace("&amp;", "&")
                          .Replace("&lt;", "<")
                          .Replace("&gt;", ">")
                          .Replace("&quot;", "\"")
                          .Replace("&#39;", "'")
                          .Replace("&nbsp;", " ");

            // Clean up whitespace: collapse 3+ newlines to 2
            result = Regex.Replace(result, @"\n{3,}", "\n\n");

            return result.Trim();
        }
    }
}
