using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DotNetCore.Extensions
{

    public static partial class Extensions
    {
        private static char[] Base64CodeArray = new char[]
        {
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
            '0', '1', '2', '3', '4',  '5', '6', '7', '8', '9', '+', '/', '='
        };

        private const string EMAIL_EXPRESSION = @"^((([a-z]|\d|[!#\$%&'\*\+\-\/=\?\^_`{\|}~]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])+(\.([a-z]|\d|[!#\$%&'\*\+\-\/=\?\^_`{\|}~]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])+)*)|((\x22)((((\x20|\x09)*(\x0d\x0a))?(\x20|\x09)+)?(([\x01-\x08\x0b\x0c\x0e-\x1f\x7f]|\x21|[\x23-\x5b]|[\x5d-\x7e]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(\\([\x01-\x09\x0b\x0c\x0d-\x7f]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF]))))*(((\x20|\x09)*(\x0d\x0a))?(\x20|\x09)+)?(\x22)))@((([a-z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(([a-z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])([a-z]|\d|-||_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])*([a-z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])))\.)+(([a-z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])+|(([a-z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])+([a-z]+|\d|-|\.{0,1}|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])?([a-z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])))$";

        public static bool IsValidEmail(this string email)
        {
            if (string.IsNullOrEmpty(email))
                return false;

            email = email.Trim();

            return new Regex(EMAIL_EXPRESSION, RegexOptions.IgnoreCase).IsMatch(email);
        }

        public static bool IsEmpty(this string value) => string.IsNullOrEmpty(value) || string.IsNullOrWhiteSpace(value);

        public static IEnumerable<T> SplitTo<T>(this string str, params string[] separator) where T : IConvertible
        {
            foreach (var s in str.Split(separator, StringSplitOptions.None))
                yield return (T)Convert.ChangeType(s, typeof(T));
        }

        public static bool Contains(this string source, string value, StringComparison comparisonType) => source?.IndexOf(value, comparisonType) >= 0;

        public static string JoinString(this IEnumerable<string> strings, string defaultSeperator = ", ")
        {
            return strings.Aggregate(new StringBuilder(),
                (sb, s) => (sb.Length == 0 ? sb : sb.Append(defaultSeperator))
                    .Append(s), sb => sb.ToString());
        }

        public static string SafeString(this string input) => input?.ToString().Trim() ?? string.Empty;

        public static bool IsBase64String(string base64Str)
        {
            byte[] bytes = null;
            return IsBase64String(base64Str, out bytes);
        }

        public static bool IsBase64String(this string base64Str, out byte[] bytes)
        {
            bytes = null;
            if (string.IsNullOrEmpty(base64Str))
                return false;
            else
            {
                if (base64Str.Contains(","))
                    base64Str = base64Str.Split(',')[1];
                if (base64Str.Length % 4 != 0)
                    return false;
                if (base64Str.Any(c => !Base64CodeArray.Contains(c)))
                    return false;
            }

            bytes = Convert.FromBase64String(base64Str);
            return true;
        }

        public static string GetSuffixFromBase64Str(this string base64Str)
        {
            string suffix = string.Empty;
            string prefix = "data:image/";
            if (base64Str.StartsWith(prefix) && base64Str.Contains(";") && base64Str.Contains(","))
            {
                base64Str = base64Str.Split(';')[0];
                suffix = base64Str.Substring(prefix.Length);
            }
            return suffix;
        }

        public static bool IsNumericOnly(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return false;

            return str.All(char.IsDigit);
        }

        public static string EnsureMaximumLength(this string str, int maxLength, string postfix = null)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            if (str.Length <= maxLength)
                return str;

            var pLen = postfix?.Length ?? 0;

            var result = str[0..(maxLength - pLen)];
            if (!string.IsNullOrEmpty(postfix))
            {
                result += postfix;
            }

            return result;
        }

        public static string Sha256(this string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hash = sha.ComputeHash(bytes);

                return Convert.ToBase64String(hash);
            }
        }

        public static string Underscore(this string value)
            => string.Concat(value.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x : x.ToString()))
                .ToLowerInvariant();
    }
}
