#region USING_DIRECTIVES
using System;
using System.Text.RegularExpressions;
#endregion;

namespace Sharper.Common
{
    public sealed class CustomIPFormat
    {
        public string Content { get; set; }

        public CustomIPFormat(string format)
        {
            if (string.IsNullOrWhiteSpace(format))
                throw new ArgumentException("IP format cannot be null.");
            this.Content = format;
        }

        private static readonly Regex _parseRegex = new Regex(@"^(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?|\*)((\.|$)(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?|\*)){0,3}(:[0-9]{4,5})?$", RegexOptions.Compiled);

        public static bool TryParse(string str, out CustomIPFormat res)
        {
            if (_parseRegex.IsMatch(str))
            {
                res = new CustomIPFormat(str);
                return true;
            }
            else
            {
                res = null;
                return false;
            }
        }
    }
}
