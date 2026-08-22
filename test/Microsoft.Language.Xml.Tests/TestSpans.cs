using Xunit;

namespace Microsoft.Language.Xml.Tests
{
    /// <summary>
    /// The positions an editor needs: what a squiggle covers, what an edit replaces, and which node
    /// the caret is in when it sits at the very end of the buffer.
    /// </summary>
    public class TestSpans
    {
        private const string Half = "<PackageReference Include=\"A\" Version=\"12";

        private static string Slice(string text, TextSpan span)
        {
            return text.Substring(span.Start, span.Length);
        }

        [Fact]
        public void AttributeValueSpanExcludesTheQuotes()
        {
            const string Text = "<r Include=\"A\" />";
            XmlElementBaseSyntax root = Parser.ParseText(Text).Root!;

            Assert.Equal("A", Slice(Text, root.GetAttribute("Include")!.ValueSpan));
        }

        [Fact]
        public void AttributeValueSpanIsEmptyForAnEmptyValue()
        {
            const string Text = "<r Include=\"\" />";
            XmlElementBaseSyntax root = Parser.ParseText(Text).Root!;
            TextSpan span = root.GetAttribute("Include")!.ValueSpan;

            Assert.Equal(0, span.Length);
            Assert.Equal(string.Empty, Slice(Text, span));
        }

        [Fact]
        public void AttributeValueSpanReachesTheEndWhenTheClosingQuoteIsMissing()
        {
            XmlElementBaseSyntax root = Parser.ParseText(Half).Root!;

            // "subtract one from each end" would answer "1" here, putting every edit one character
            // short of what the user typed.
            Assert.Equal("12", Slice(Half, root.GetAttribute("Version")!.ValueSpan));
        }

        [Fact]
        public void ContentSpanCoversWhatIsBetweenTheTags()
        {
            const string Text = "<r><v>abc</v></r>";
            XmlElementBaseSyntax root = Parser.ParseText(Text).Root!;

            Assert.Equal("abc", Slice(Text, root.GetElement("v")!.ContentSpan));
        }

        [Fact]
        public void ContentSpanIsEmptyButPositionedForAnElementWithNoContent()
        {
            const string Text = "<r><v></v></r>";
            XmlElementBaseSyntax root = Parser.ParseText(Text).Root!;
            TextSpan span = root.GetElement("v")!.ContentSpan;

            Assert.Equal(0, span.Length);
            Assert.Equal(Text.IndexOf("</v>"), span.Start);
        }

        [Fact]
        public void ContentSpanOfASelfClosingElementSitsWhereContentWouldStart()
        {
            const string Text = "<r><v /></r>";
            XmlElementBaseSyntax root = Parser.ParseText(Text).Root!;
            TextSpan span = root.GetElement("v")!.ContentSpan;

            Assert.Equal(0, span.Length);
            Assert.Equal(Text.IndexOf("/>"), span.Start);
        }

        [Fact]
        public void FindNodeAtTheEndOfTheBufferAnswersWithTheNodeTheCaretIsIn()
        {
            XmlDocumentSyntax document = Parser.ParseText(Half);

            SyntaxNode atEnd = document.FindNode(Half.Length, null, includeTrivia: true, excludeTerminal: false);
            SyntaxNode oneBack = document.FindNode(Half.Length - 1, null, includeTrivia: true, excludeTerminal: false);

            // Both sit inside the value being typed; before this, the caret at the end fell all the
            // way back to the document.
            Assert.IsType<XmlTextTokenSyntax>(atEnd);
            Assert.Same(oneBack.Parent, atEnd.Parent);
        }

        [Fact]
        public void FindNodeInTheMiddleIsUnchanged()
        {
            const string Text = "<r Include=\"A\" />";
            XmlDocumentSyntax document = Parser.ParseText(Text);

            SyntaxNode node = document.FindNode(Text.IndexOf("Include"), null, includeTrivia: true, excludeTerminal: false);

            Assert.Equal("Include", node.ToString());
        }

        [Fact]
        public void NameSpanCoversTheNameInTheStartTag()
        {
            const string Text = "<r><PropertyGroup>x</PropertyGroup></r>";
            XmlElementBaseSyntax root = Parser.ParseText(Text).Root!;

            Assert.Equal("PropertyGroup", Slice(Text, root.GetElement("PropertyGroup")!.NameSpan));
        }

        [Fact]
        public void NameSpanCoversAPrefixedNameWhole()
        {
            const string Text = "<p:r xmlns:p=\"u\" />";
            XmlElementBaseSyntax root = Parser.ParseText(Text).Root!;

            Assert.Equal("p:r", Slice(Text, root.NameSpan));
        }

        [Fact]
        public void NameSpanIsEmptyButPositionedForATagStillBeingTyped()
        {
            const string Text = "< />";
            TextSpan span = Parser.ParseText(Text).Root!.NameSpan;

            // Just past the "<" and its trailing space is where the name being typed goes.
            Assert.Equal(0, span.Length);
            Assert.Equal(2, span.Start);
        }

        [Fact]
        public void StartAndEndTagNameSpansAreTheRenamePair()
        {
            const string Text = "<r><v>abc</v></r>";
            var element = (XmlElementSyntax)Parser.ParseText(Text).Root!.GetElement("v")!;

            Assert.Equal(Text.IndexOf("<v>") + 1, element.StartTag.NameSpan.Start);
            Assert.Equal("v", Slice(Text, element.StartTag.NameSpan));
            Assert.Equal(Text.IndexOf("</v>") + 2, element.EndTag.NameSpan.Start);
            Assert.Equal("v", Slice(Text, element.EndTag.NameSpan));
        }

        [Fact]
        public void EndTagNameSpanOfAMissingEndTagSitsWhereTheTagWouldGo()
        {
            const string Text = "<r>x";
            var element = (XmlElementSyntax)Parser.ParseText(Text).Root!;
            TextSpan span = element.EndTag.NameSpan;

            // The end tag is synthesized at the end of the buffer, which is where completing it
            // inserts.
            Assert.Equal(0, span.Length);
            Assert.Equal(Text.Length, span.Start);
        }

        [Fact]
        public void TextSpanDeconstructsIntoStartAndLength()
        {
            var (start, length) = new TextSpan(3, 4);

            Assert.Equal(3, start);
            Assert.Equal(4, length);
        }
    }
}
