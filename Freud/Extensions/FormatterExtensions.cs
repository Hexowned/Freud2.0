#region USING_DIRECTIVES

using System.Text.RegularExpressions;

#endregion USING_DIRECTIVES

namespace Freud.Extensions
{
    public static class FormatterExtensions
    {
        private static readonly Regex MarkdownStripRegex = new Regex(@"([`\*_~\[\]\(\)""])", RegexOptions.ECMAScript);

        public static string Spoiler(string str)
            => $"||{str}||";

        public static string StripMarkdown(string str)
            => MarkdownStripRegex.Replace(str, m => string.Empty);
    }
}
