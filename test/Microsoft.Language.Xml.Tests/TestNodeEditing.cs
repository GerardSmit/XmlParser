using System.Linq;
using Microsoft.Language.Xml.Collections;
using Xunit;
using static Microsoft.Language.Xml.SyntaxFactory;

namespace Microsoft.Language.Xml.Tests
{
    /// <summary>
    /// The node-level editing and inspection members - the ones a caller reaches for between the
    /// path helpers and the raw factories.
    /// </summary>
    public class TestNodeEditing
    {
        private const string Document = "<r>\n  <a />\n  <b />\n  <c />\n</r>";

        private static XmlElementBaseSyntax Root(string text = Document)
        {
            return Parser.ParseText(text).Root!;
        }

        // ---------------------------------------------------------------- children

        [Fact]
        public void RemoveChildTakesTheChildAndItsLine()
        {
            XmlElementBaseSyntax root = Root();

            XmlElementSyntax removed = root.RemoveChild(root.GetElement("b")!);

            Assert.Null(removed.GetElement("b"));
            Assert.Equal(new[] { "a", "c" }, removed.Elements.Select(x => x.Name));

            // The child's own leading trivia goes with it, so the line it was on closes up
            // rather than being left behind as a blank one.
            Assert.Equal("<r>\n  <a />\n  <c />\n</r>", removed.ToFullString());
        }

        [Theory]
        [InlineData(0, "added,a,b,c")]
        [InlineData(1, "a,added,b,c")]
        [InlineData(2, "a,b,added,c")]
        public void InsertChildPutsTheChildWhereItIsAsked(int index, string expected)
        {
            XmlElementSyntax edited = Root().InsertChild(XmlEmptyElement("added"), index);

            Assert.Equal(expected, string.Join(",", edited.Elements.Select(x => x.Name)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void InsertChildIndentsWhereverItLands(int index)
        {
            XmlElementSyntax edited = Root().InsertChild(XmlEmptyElement("added"), index);

            Assert.Equal("\n  ", edited.GetElement("added")!.GetLeadingTrivia().ToFullString());
        }

        [Fact]
        public void AddChildAlignsTheEndTagOfAHandBuiltSubtree()
        {
            // AddElement on a detached subtree leaves a bare line break in front of the end tag -
            // a placeholder for an element nobody has placed yet. Once AddChild gives the subtree
            // its real line, the end tag belongs under the start tag it closes.
            var built = (XmlElementSyntax)Parser.ParseText("<added></added>").Root!;
            built = built.AddElement("child", out _);

            XmlElementSyntax edited = ((XmlElementSyntax)Root()).AddChild(built);
            var added = (XmlElementSyntax)edited.GetElement("added")!;

            Assert.Equal("\n  ", added.EndTag.GetLeadingTrivia().ToFullString());
        }

        [Fact]
        public void AddChildKeepsAnAuthoredEndTagIndent()
        {
            // Only the bare placeholder is realigned. An end tag someone gave an indent of its own
            // - however odd - is the document's text, and AddChild edits as little of it as it can.
            var built = (XmlElementSyntax)Parser.ParseText("<added>\n   <child />\n   </added>").Root!;

            XmlElementSyntax edited = ((XmlElementSyntax)Root()).AddChild(built);
            var added = (XmlElementSyntax)edited.GetElement("added")!;

            Assert.Equal("\n   ", added.EndTag.GetLeadingTrivia().ToFullString());
        }

        // ---------------------------------------------------------------- attributes

        [Fact]
        public void AddAttributeAppendsTheNodeAsGiven()
        {
            XmlElementBaseSyntax root = Root("<r />").AddAttribute(XmlAttribute("a", "1"));

            Assert.Equal("1", root.GetAttributeValue("a"));
        }

        [Fact]
        public void AddAttributesAppendsAllOfThem()
        {
            XmlElementBaseSyntax root = Root("<r />").AddAttributes(XmlAttribute("a", "1"), XmlAttribute("b", "2"));

            Assert.Equal(new[] { "a", "b" }, root.Attributes.Select(x => x.Name));
        }

        [Fact]
        public void RemoveAttributeTakesOnlyThatOne()
        {
            XmlElementBaseSyntax root = Root("<r a=\"1\" b=\"2\" />");

            XmlElementBaseSyntax removed = root.RemoveAttribute(root.GetAttribute("a")!);

            Assert.Null(removed.GetAttribute("a"));
            Assert.Equal("2", removed.GetAttributeValue("b"));
        }

        [Fact]
        public void TheStringFactoriesSplitAPrefixedName()
        {
            // The text alone does not tell an accepted name from a flat token: both write
            // "<p:a q:x="1" />". The tree does, and it is the tree every lookup asks.
            XmlEmptyElementSyntax element = XmlEmptyElement("p:a", XmlAttribute("q:x", "1"));

            Assert.Equal("a", element.NameNode.LocalName);
            Assert.Equal("p", element.NameNode.Prefix);
            Assert.Equal("<p:a q:x=\"1\" />", element.ToFullString());
            Assert.NotNull(element.GetAttribute("x", "q"));

            XmlElementBaseSyntax withContent = XmlElement("p:a", "text");

            Assert.Equal("p", withContent.NameNode.Prefix);
            Assert.Equal("<p:a>text</p:a>", withContent.ToFullString());

            // And the element is found on the tree the edit returned, without a reparse.
            Assert.NotNull(Root("<r />").AddChild(element).GetElement("a", "p"));
        }

        [Fact]
        public void AnAttributeCanBeRenamed()
        {
            XmlAttributeSyntax attribute = Root("<p:r xmlns:p=\"urn:p\" p:a=\"1\" />").GetAttribute("a", "p")!;

            Assert.Equal("q:a=\"1\"", attribute.WithPrefixName("q").ToFullString().Trim());
            Assert.Equal("p:b=\"1\"", attribute.WithLocalName("b").ToFullString().Trim());
        }

        [Fact]
        public void AnElementCanBeRenamed()
        {
            XmlElementBaseSyntax element = Root("<p:r xmlns:p=\"urn:p\">\n  <p:a />\n</p:r>").GetElement("a", "p")!;

            Assert.Equal("<q:a />", element.WithPrefixName("q").ToFullString().Trim());
        }

        // ---------------------------------------------------------------- enumerator state

        [Fact]
        public void TheAttributeEnumeratorReportsWhereItIs()
        {
            XmlAttributeNodeEnumerator attributes = Root("<r a=\"1\" b=\"2\" />").Attributes;

            // The index of Current, so there is none until there is a Current.
            Assert.Equal(-1, attributes.CurrentIndex);
            Assert.True(attributes.MoveNext());
            Assert.Equal(0, attributes.CurrentIndex);
            Assert.True(attributes.MoveNext());
            Assert.Equal(1, attributes.CurrentIndex);
            Assert.False(attributes.MoveNext());
        }

        [Fact]
        public void ResettingAnEnumeratorForgetsWhereItWas()
        {
            XmlAttributeNodeEnumerator attributes = Root("<r a=\"1\" b=\"2\" />").Attributes;

            Assert.True(attributes.MoveNext());
            attributes.Reset();

            Assert.Null(attributes.Current);
            Assert.Equal(-1, attributes.CurrentIndex);
        }

        // ---------------------------------------------------------------- spans on broken input

        [Fact]
        public void ContentSpanHoldsUpWithNoEndTag()
        {
            // The end tag is synthesized and zero-width, so the span has nothing to run backwards
            // from - it collapses at the end of the start tag instead.
            XmlElementBaseSyntax root = Root("<r>text");

            TextSpan span = root.ContentSpan;

            Assert.True(span.Length >= 0);
            Assert.Equal("text", "<r>text".Substring(span.Start, span.Length));
        }

        [Fact]
        public void ContentSpanHoldsUpOnAnElementBuiltWithNoEndTagAtAll()
        {
            // The parser always synthesizes one, but XmlElement takes a null end tag without so
            // much as an assertion, so a hand-built tree can have none. Asking a node for its span
            // is not the place to find that out by exception. (XmlEmptyElement is different: it
            // asserts its "/>" is there, so a missing one is a caller error, not a shape.)
            XmlElementSyntax element = XmlElement(
                XmlElementStartTag(LessThan, XmlName("a"), null, GreaterThan),
                default(SyntaxNode),
                null);

            Assert.Equal(0, element.ContentSpan.Length);
            Assert.Equal(string.Empty, element.RawValue);
            Assert.Equal(string.Empty, element.Value);
        }

        [Fact]
        public void ValueSpanOfAHandBuiltAttributeSitsJustPastTheName()
        {
            // A null value is the bare-name attribute an editor leaves mid-keystroke, and the
            // parameter is nullable to say so - no "null!" needed here.
            XmlAttributeSyntax attribute = XmlElement("a", XmlAttribute("x", null)).GetAttribute("x")!;

            Assert.Equal(0, attribute.ValueSpan.Length);
            Assert.Equal(attribute.Span.End, attribute.ValueSpan.Start);
        }

        // ---------------------------------------------------------------- document entry points

        [Fact]
        public void TheDocumentAnswersByLocalNameToo()
        {
            XmlDocumentSyntax document = Parser.ParseText("<p:r xmlns:p=\"urn:p\">\n  <p:a />\n  <b />\n</p:r>");

            Assert.NotNull(document.GetElementByLocalName("r"));
            Assert.Single(document.GetElementsByLocalName("r"));
            Assert.Equal(new[] { "p:a" }, document.DescendantsByLocalName("a").Select(x => x.Name));
        }

        [Fact]
        public void TheDocumentFiltersDescendantsByPrefixAlone()
        {
            XmlDocumentSyntax document = Parser.ParseText("<p:r xmlns:p=\"urn:p\">\n  <p:a />\n  <b />\n</p:r>");

            Assert.Equal(new[] { "p:r", "p:a" }, document.Descendants(null, "p").Select(x => x.Name));
        }
    }
}
