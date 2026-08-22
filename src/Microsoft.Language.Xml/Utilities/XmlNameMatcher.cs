using System;

namespace Microsoft.Language.Xml
{
    /// <summary>
    /// The one place that decides whether a name in the document is the name a caller asked for,
    /// so every lookup answers the same question the same way.
    /// </summary>
    internal static class XmlNameMatcher
    {
        public static bool Matches(XmlNameSyntax name, string localName, string? prefix, bool matchAnyPrefix, StringComparison comparison)
        {
            if (!string.Equals(name.LocalName, localName, comparison))
            {
                return false;
            }

            return matchAnyPrefix || PrefixMatches(name.Prefix, prefix, comparison);
        }

        /// <summary>
        /// Compares prefixes, treating <c>null</c> and the empty string as the same thing.
        /// </summary>
        /// <remarks>
        /// An unprefixed name reports a <c>null</c> prefix, but the empty string is what a caller
        /// reaches for to say "no prefix" - and it is what lands there by accident when
        /// <c>GetAttributeValue(name, string.Empty)</c> is misread as taking a default value.
        /// Either spelling meaning the same thing removes a silent miss.
        /// </remarks>
        public static bool PrefixMatches(string? actual, string? expected, StringComparison comparison)
        {
            if (string.IsNullOrEmpty(actual))
            {
                return string.IsNullOrEmpty(expected);
            }

            return !string.IsNullOrEmpty(expected) && string.Equals(actual, expected, comparison);
        }

        /// <summary>
        /// Rejects a name the document could not read back as the name it was given: one that is
        /// empty, that starts where XML does not let a name start, or that holds a character XML
        /// gives a meaning of its own. Writing one produces a document that no longer parses, or
        /// worse, one that parses as something else - an element named "a b" comes back as an
        /// element "a" carrying an attribute "b", which no lookup for "a b" will ever find again.
        /// </summary>
        /// <remarks>
        /// The rule is the scanner's own character tables rather than one written here. What makes
        /// a name readable back is exactly what the thing doing the reading accepts, and any second
        /// opinion about it is wrong in one direction or the other: a hand-written whitelist
        /// rejects an ordinary Devanagari or Thai name, and a blacklist lets through a lone
        /// surrogate or a stray ";" that quietly ends the name early.
        /// </remarks>
        /// <param name="name">
        /// One half of a name - a prefix or a local name, never "prefix:local". <c>null</c> is
        /// accepted, for the prefix an unprefixed name does not have.
        /// </param>
        /// <param name="whole">The name as the caller wrote it, for the message.</param>
        public static void Validate(string? name, string whole, string paramName)
        {
            if (name is null)
            {
                return;
            }

            if (name.Length == 0 || !IsNameCharacterAt(name, 0, start: true))
            {
                throw new ArgumentException($"'{whole}' is not an XML name.", paramName);
            }

            for (var i = 1; i < name.Length; i++)
            {
                // The low half of a pair the previous step already accepted as one character.
                if (char.IsLowSurrogate(name[i]) && char.IsHighSurrogate(name[i - 1]))
                {
                    continue;
                }

                if (!IsNameCharacterAt(name, i, start: false))
                {
                    throw new ArgumentException($"'{whole}' is not an XML name.", paramName);
                }
            }
        }

        /// <summary>
        /// Validates a name that may carry a prefix, splitting it the way the document will.
        /// </summary>
        public static void ValidateQualified(string name, string paramName)
        {
            var colon = name.IndexOf(':');

            Validate(colon < 0 ? null : name.Substring(0, colon), name, paramName);
            Validate(colon < 0 ? name : name.Substring(colon + 1), name, paramName);
        }

        private static bool IsNameCharacterAt(string name, int index, bool start)
        {
            var c = name[index];

            // A surrogate pair is one name character to the scanner - but only up to U+EFFFF, the
            // range XML gives names. Half of a pair is not a character at all, and writing one
            // produces a name that ends where it was cut. The upper bound matters as much: a
            // private-use character from plane 15 is a well-formed pair that the scanner still
            // refuses, so accepting it here writes a document neither this parser nor
            // System.Xml will read back.
            if (char.IsHighSurrogate(c))
            {
                if (index + 1 >= name.Length || !char.IsLowSurrogate(name[index + 1]))
                {
                    return false;
                }

                var codePoint = char.ConvertToUtf32(c, name[index + 1]);

                return codePoint <= 0xEFFFF;
            }

            if (char.IsLowSurrogate(c))
            {
                return false;
            }

            // NCName, not Name: a prefix is split off before this runs, so a colon left in either
            // half is a name that was never going to read back the way it was written.
            return start ? XmlCharType.IsStartNCNameSingleChar(c) : XmlCharType.IsNCNameSingleChar(c);
        }
    }
}
