using System;

namespace Microsoft.Language.Xml
{
    /// <summary>
    /// Shared parsing rules for the slash-separated child paths taken by <see cref="XmlExtensions"/>.
    /// One place, so the selecting and the creating APIs cannot drift apart on what a path means.
    /// </summary>
    internal static class XmlPath
    {
        /// <summary>
        /// Validates <paramref name="path"/> and returns the index its first segment starts at.
        /// </summary>
        /// <remarks>
        /// A single leading slash is accepted and skipped: it is what anyone with XPath habits types
        /// first, and paths here are always relative to the node they are handed to, so "/a/b" can
        /// only sensibly mean "a/b". Everything else that would produce a nameless segment - an empty
        /// path, a trailing slash, a doubled slash - is rejected here rather than silently turned
        /// into an element with no name.
        /// </remarks>
        /// <param name="validateNames">
        /// Whether each segment must also be a name an element can be created with. A path that
        /// only selects can hold anything - it simply matches nothing - but a path that creates
        /// writes its segments into the document, and a segment such as "a b" comes back as an
        /// element "a" carrying an attribute "b", which the next call will not find and will
        /// create again. Checking on the way in turns a silently duplicating get-or-add into an
        /// <see cref="ArgumentException"/> at the call that got it wrong.
        /// </param>
        /// <exception cref="ArgumentException">
        /// The path is empty, contains an empty segment, or - when <paramref name="validateNames"/>
        /// is set - contains a segment that is not an XML name.
        /// </exception>
        public static int Validate(string path, string paramName, out int segmentCount, bool validateNames = false)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path must not be empty.", paramName);
            }

            var start = path[0] == '/' ? 1 : 0;

            if (start == path.Length)
            {
                throw new ArgumentException("Path must not be empty.", paramName);
            }

            segmentCount = 1;

            var segmentStart = start;

            for (var i = start; i < path.Length; i++)
            {
                if (path[i] != '/')
                {
                    continue;
                }

                if (i == start || i == path.Length - 1 || path[i - 1] == '/')
                {
                    throw new ArgumentException($"Path '{path}' contains an empty segment.", paramName);
                }

                if (validateNames)
                {
                    XmlNameMatcher.ValidateQualified(path.Substring(segmentStart, i - segmentStart), paramName);
                }

                segmentStart = i + 1;
                segmentCount++;
            }

            if (validateNames)
            {
                XmlNameMatcher.ValidateQualified(path.Substring(segmentStart), paramName);
            }

            return start;
        }
    }
}
