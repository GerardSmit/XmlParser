using System;
using System.Globalization;
using System.Text;

namespace Microsoft.Language.Xml
{
    /// <summary>
    /// Converts between the text of an XML document and the string a caller means by it.
    /// </summary>
    /// <remarks>
    /// Only the five entities XML predefines are handled, plus numeric character references. The
    /// wider HTML entity set is deliberately not: <c>&amp;nbsp;</c> is undefined in XML without a
    /// DTD, so resolving it would invent a character the document does not contain.
    /// </remarks>
    public static class XmlEscaping
    {
        /// <summary>
        /// The characters between "&amp;" and ";" in the longest reference there is, "&amp;#x10FFFF;",
        /// once any leading zeros are discounted.
        /// </summary>
        private const int MaxReferenceLength = 8;

        /// <summary>
        /// Escapes a string so it can be used as element text: <c>&amp;</c> and <c>&lt;</c> become
        /// entities, and a <c>&gt;</c> is escaped when it would close a CDATA section.
        /// </summary>
        public static string EncodeText(string value)
        {
            return EncodeText(value, null);
        }

        /// <inheritdoc cref="EncodeText(string)"/>
        /// <param name="preceding">
        /// The text this string is about to be written after, if any. A "]]&gt;" split across two
        /// strings closes a CDATA section just as surely as one written in a single string, and
        /// each string on its own cannot see it coming.
        /// </param>
        internal static string EncodeText(string value, string? preceding)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            StringBuilder? builder = null;

            for (var i = 0; i < value.Length; i++)
            {
                var replacement = value[i] switch
                {
                    '&' => "&amp;",
                    '<' => "&lt;",
                    // Only the ">" that ends a "]]>" has to go; escaping every one would rewrite
                    // text that is perfectly legal as it stands.
                    '>' when CharAt(value, preceding, i - 1) == ']' && CharAt(value, preceding, i - 2) == ']' => "&gt;",
                    // A literal CR in text is normalized to LF by any conforming reader, so it has
                    // to be written as a character reference to survive a round trip.
                    '\r' => "&#xD;",
                    _ => null
                };

                Append(ref builder, value, i, replacement);
            }

            return builder?.ToString() ?? value;
        }

        /// <summary>
        /// The character at <paramref name="index"/>, reading back into <paramref name="preceding"/>
        /// when the index runs off the front of <paramref name="value"/>.
        /// </summary>
        private static char CharAt(string value, string? preceding, int index)
        {
            if (index >= 0)
            {
                return value[index];
            }

            var from = (preceding?.Length ?? 0) + index;

            return from >= 0 ? preceding![from] : '\0';
        }

        /// <summary>
        /// Escapes a string so it can be used as an attribute value inside the given quote
        /// character. The other quote character is left alone, so the result stays as close to what
        /// the caller wrote as the format allows.
        /// </summary>
        public static string EncodeAttributeValue(string value, char quote = '"')
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            StringBuilder? builder = null;

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                var replacement = c switch
                {
                    '&' => "&amp;",
                    '<' => "&lt;",
                    '"' when quote == '"' => "&quot;",
                    '\'' when quote == '\'' => "&apos;",
                    // A literal tab or line break in an attribute value is normalized to a space by
                    // any conforming reader, so it has to be written as a character reference to
                    // survive a round trip.
                    '\t' => "&#x9;",
                    '\n' => "&#xA;",
                    '\r' => "&#xD;",
                    _ => null
                };

                Append(ref builder, value, i, replacement);
            }

            return builder?.ToString() ?? value;
        }

        /// <summary>
        /// Normalizes line endings the way XML 1.0 section 2.11 says a reader must: a literal CRLF
        /// or a lone CR in the document stands for a single LF.
        /// </summary>
        /// <remarks>
        /// This is about the characters in the document, not the ones a reference names, so it
        /// runs before references are resolved - "&amp;#xD;" is how a document says it means a
        /// carriage return, and normalizing it away would take the meaning with it.
        /// </remarks>
        public static string NormalizeLineEndings(string value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var start = value.IndexOf('\r');

            if (start < 0)
            {
                return value;
            }

            var builder = new StringBuilder(value.Length);
            builder.Append(value, 0, start);

            for (var i = start; i < value.Length; i++)
            {
                if (value[i] != '\r')
                {
                    builder.Append(value[i]);
                    continue;
                }

                builder.Append('\n');

                // The pair is one line ending, not two.
                if (i + 1 < value.Length && value[i + 1] == '\n')
                {
                    i++;
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Resolves the five predefined entities and numeric character references, having first
        /// normalized line endings. Anything else - an entity the document declares itself, or
        /// plain malformed text - is left exactly as it was found.
        /// </summary>
        public static string Decode(string value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            value = NormalizeLineEndings(value);

            var start = value.IndexOf('&');

            if (start < 0)
            {
                return value;
            }

            var builder = new StringBuilder(value.Length);
            builder.Append(value, 0, start);

            for (var i = start; i < value.Length; i++)
            {
                if (value[i] != '&')
                {
                    builder.Append(value[i]);
                    continue;
                }

                var end = FindReferenceEnd(value, i);

                if (end < 0 || !TryDecodeReference(value, i + 1, end, builder))
                {
                    builder.Append('&');
                    continue;
                }

                i = end;
            }

            return builder.ToString();
        }

        /// <summary>
        /// The index of the ";" ending the reference that starts at <paramref name="ampersand"/>,
        /// or -1. The search is bounded rather than running to the end of the string for every
        /// stray "&amp;", which would make decoding quadratic on text full of them.
        /// </summary>
        private static int FindReferenceEnd(string value, int ampersand)
        {
            var start = ampersand + 1;

            // Leading zeros are legal in a character reference and say nothing, so they are skipped
            // rather than counted against the bound: "&#x0041;" is "&#x41;".
            if (start < value.Length && value[start] == '#')
            {
                var digits = start + 1;

                if (digits < value.Length && (value[digits] == 'x' || value[digits] == 'X'))
                {
                    digits++;
                }

                while (digits < value.Length && value[digits] == '0')
                {
                    digits++;
                }

                start = digits;
            }

            if (start >= value.Length)
            {
                return -1;
            }

            var limit = Math.Min(value.Length, start + MaxReferenceLength + 1);

            return value.IndexOf(';', start, limit - start);
        }

        /// <summary>
        /// Appends the reference between <paramref name="start"/> and <paramref name="end"/>,
        /// exclusive of the surrounding <c>&amp;</c> and <c>;</c>, reporting whether it was one
        /// this method knows.
        /// </summary>
        private static bool TryDecodeReference(string value, int start, int end, StringBuilder builder)
        {
            var length = end - start;

            if (length <= 0)
            {
                return false;
            }

            if (value[start] == '#')
            {
                return TryDecodeNumericReference(value, start + 1, end, builder);
            }

            switch (length)
            {
                case 2 when string.CompareOrdinal(value, start, "lt", 0, 2) == 0:
                    builder.Append('<');
                    return true;
                case 2 when string.CompareOrdinal(value, start, "gt", 0, 2) == 0:
                    builder.Append('>');
                    return true;
                case 3 when string.CompareOrdinal(value, start, "amp", 0, 3) == 0:
                    builder.Append('&');
                    return true;
                case 4 when string.CompareOrdinal(value, start, "quot", 0, 4) == 0:
                    builder.Append('"');
                    return true;
                case 4 when string.CompareOrdinal(value, start, "apos", 0, 4) == 0:
                    builder.Append('\'');
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryDecodeNumericReference(string value, int start, int end, StringBuilder builder)
        {
            var hex = start < end && (value[start] == 'x' || value[start] == 'X');

            if (hex)
            {
                start++;
            }

            if (start >= end)
            {
                return false;
            }

#if NETSTANDARD2_0
            var digits = value.Substring(start, end - start);
#else
            ReadOnlySpan<char> digits = value.AsSpan(start, end - start);
#endif

            // AllowHexSpecifier rather than HexNumber: the latter also allows surrounding
            // whitespace, which would accept "&#x 41 ;" - not a reference XML recognizes.
            var style = hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None;

            if (!int.TryParse(digits, style, CultureInfo.InvariantCulture, out var codePoint) || !IsXmlCharacter(codePoint))
            {
                return false;
            }

            builder.Append(char.ConvertFromUtf32(codePoint));
            return true;
        }

        /// <summary>
        /// The characters XML allows a document to contain. A reference to anything else - a NUL,
        /// a control character, half of a surrogate pair - is not text, so it is left as written
        /// rather than turned into something the document could not have held in the first place.
        /// </summary>
        private static bool IsXmlCharacter(int codePoint)
        {
            return codePoint == 0x9
                || codePoint == 0xA
                || codePoint == 0xD
                || (codePoint >= 0x20 && codePoint <= 0xD7FF)
                || (codePoint >= 0xE000 && codePoint <= 0xFFFD)
                || (codePoint >= 0x10000 && codePoint <= 0x10FFFF);
        }

        /// <summary>
        /// Copies <paramref name="value"/> into the builder lazily: nothing is allocated until the
        /// first character that actually has to change.
        /// </summary>
        private static void Append(ref StringBuilder? builder, string value, int index, string? replacement)
        {
            if (replacement is null)
            {
                builder?.Append(value[index]);
                return;
            }

            builder ??= new StringBuilder(value.Length + 8).Append(value, 0, index);
            builder.Append(replacement);
        }
    }
}
