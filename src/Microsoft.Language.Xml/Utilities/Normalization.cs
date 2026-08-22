using System.Text;

namespace Microsoft.Language.Xml.Utilities
{
    public static class Normalization
    {
        /// <summary>
        /// Get normalized value
        /// </summary>
        /// <param name="value"><see cref="string"/> to normalize.</param>
        /// <remarks>
        /// Every tab and line end in an attribute value is a space, per XML 1.0 section 3.3.3,
        /// with line ends normalized first so a CRLF counts once. A tab or line break the document
        /// actually means is written as a character reference, and this runs before references are
        /// resolved so that one survives.
        /// <br/>
        /// Normalization specs:
        /// <seealso href="https://www.w3.org/TR/2006/REC-xml11-20060816/#sec-line-ends">2.2.12 [XML] Section 3.3.3</seealso>
        /// <seealso href="https://learn.microsoft.com/en-us/openspecs/ie_standards/ms-xml/389b8ef1-e19e-40ac-80de-eec2cd0c58ae">2.11 [XML] End-of-Line Handling</seealso>
        /// </remarks>
        public static string GetNormalizedAttributeValue(this string value) =>
            GetNormalizedAttributeValue(new StringBuilder(value));

        internal static string GetNormalizedAttributeValue(StringBuilder inputBuffer)
        {
            var outputBuffer = PooledStringBuilder.GetInstance();
            NormalizeAttributeValueTo(inputBuffer, outputBuffer);
            return outputBuffer.ToStringAndFree();
        }

        internal static string GetNormalizedAttributeValue(this SyntaxNode node)
        {
            var inputBuffer = PooledStringBuilder.GetInstance();
            var writer = new System.IO.StringWriter(inputBuffer.Builder, System.Globalization.CultureInfo.InvariantCulture);
            node.WriteTo(writer);
            var outputBuffer = PooledStringBuilder.GetInstance();
            inputBuffer.Builder.NormalizeAttributeValueTo(outputBuffer);
            inputBuffer.Free();
            return outputBuffer.ToStringAndFree();
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static void NormalizeAttributeValueTo(this StringBuilder inputBuffer, PooledStringBuilder outputBuffer)
        {
            var inputBufferLength = inputBuffer.Length;
            for (int charIndex = 0; charIndex < inputBufferLength; charIndex++)
            {
                var c = inputBuffer[charIndex];
                switch (c)
                {
                    // Line ends are normalized first (XML 1.0 section 2.11), so a CRLF is one
                    // line ending and becomes one space rather than two.
                    case '\r' when charIndex + 1 < inputBufferLength && inputBuffer[charIndex + 1] == '\n':
                        outputBuffer.Builder.Append(' ');
                        charIndex++;
                        break;
                    // Every remaining line end and tab is a space in its own right - there is no
                    // "the last one was whitespace too" exception, so "a\n\nb" keeps both.
                    case '\r':
                    case '\n':
                    case '\t':
                        outputBuffer.Builder.Append(' ');
                        break;
                    // NEL and LINE SEPARATOR are line ends in XML 1.1 only. This parser reads
                    // XML 1.0, where they are ordinary characters, and turning them into spaces
                    // rewrites text the document meant to hold.
                    default:
                        outputBuffer.Builder.Append(c);
                        break;
                }
            }
        }
    }
}