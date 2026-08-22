namespace Microsoft.Language.Xml
{
    /// <summary>
    /// Conversions between this library's <see cref="TextSpan"/> and Roslyn's
    /// <see cref="CodeAnalysis.Text.TextSpan"/>. The two share a simple name, so a file that
    /// names both needs an alias for whichever it names second; going through these methods, a
    /// consumer rarely has to name the other one at all.
    /// </summary>
    public static class RoslynTextSpanExtensions
    {
        /// <summary>
        /// This span as a <see cref="CodeAnalysis.Text.TextSpan"/>, ready for
        /// <c>SourceText.Lines</c>, an LSP range or a <c>TextEdit</c>.
        /// </summary>
        public static CodeAnalysis.Text.TextSpan ToRoslynSpan(this TextSpan span)
        {
            return new CodeAnalysis.Text.TextSpan(span.Start, span.Length);
        }

        /// <summary>
        /// A Roslyn span as this library's <see cref="TextSpan"/>, for handing an editor range
        /// back to <see cref="SyntaxLocator"/> and the other span-taking APIs.
        /// </summary>
        public static TextSpan ToXmlSpan(this CodeAnalysis.Text.TextSpan span)
        {
            return new TextSpan(span.Start, span.Length);
        }
    }
}
