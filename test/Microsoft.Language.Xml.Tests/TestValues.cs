using Xunit;
using static Microsoft.Language.Xml.SyntaxFactory;

namespace Microsoft.Language.Xml.Tests
{
    /// <summary>
    /// Escaping on the way in, decoding on the way out, and the raw text still reachable in between.
    /// </summary>
    public class TestValues
    {
        [Theory]
        [InlineData("A&B<C", "A&amp;B&lt;C")]
        [InlineData("plain", "plain")]
        [InlineData("]]>", "]]&gt;")]
        [InlineData("a > b", "a > b")]
        [InlineData("a\rb", "a&#xD;b")]
        public void EncodeTextEscapesWhatMustBeEscaped(string value, string expected)
        {
            Assert.Equal(expected, XmlEscaping.EncodeText(value));
        }

        [Theory]
        [InlineData("A&B<C", '"', "A&amp;B&lt;C")]
        [InlineData("a\"b", '"', "a&quot;b")]
        [InlineData("a\"b", '\'', "a\"b")]
        [InlineData("a'b", '\'', "a&apos;b")]
        [InlineData("a'b", '"', "a'b")]
        [InlineData("a\tb", '"', "a&#x9;b")]
        public void EncodeAttributeValueEscapesTheQuoteInUse(string value, char quote, string expected)
        {
            Assert.Equal(expected, XmlEscaping.EncodeAttributeValue(value, quote));
        }

        [Theory]
        [InlineData("A&amp;B", "A&B")]
        [InlineData("&lt;&gt;&quot;&apos;", "<>\"'")]
        [InlineData("&#65;&#x42;", "AB")]
        [InlineData("nothing to do", "nothing to do")]
        // Unrecognized references are left exactly as found: &nbsp; is not defined in XML without
        // a DTD, so resolving it would invent a character the document does not contain.
        [InlineData("&nbsp;", "&nbsp;")]
        [InlineData("a & b", "a & b")]
        [InlineData("&#xZZ;", "&#xZZ;")]
        [InlineData("&", "&")]
        // NumberStyles.HexNumber would accept the whitespace here; XML does not.
        [InlineData("&#x 41 ;", "&#x 41 ;")]
        // Not characters a document could have held in the first place.
        [InlineData("&#0;", "&#0;")]
        [InlineData("&#xD800;", "&#xD800;")]
        // Longer than any reference there is, so the ";" is not even looked for.
        [InlineData("&ThisIsFarTooLongToBeAReference;", "&ThisIsFarTooLongToBeAReference;")]
        [InlineData("&#x10FFFF;", "\U0010FFFF")]
        // Leading zeros are legal and say nothing, so they do not count against the bound.
        [InlineData("&#x0000000041;", "A")]
        [InlineData("&#000000000065;", "A")]
        [InlineData("&#x00000000110000;", "&#x00000000110000;")]
        public void DecodeResolvesOnlyWhatXmlDefines(string value, string expected)
        {
            Assert.Equal(expected, XmlEscaping.Decode(value));
        }

        [Fact]
        public void DecodeRoundTripsEncodeText()
        {
            const string Value = "a < b & c ]]> d";

            Assert.Equal(Value, XmlEscaping.Decode(XmlEscaping.EncodeText(Value)));
        }

        [Fact]
        public void SetAttributeEscapesTheValue()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<r />").Root;

            root = root.SetAttribute("Include", "A&B<C");

            Assert.Equal("<r Include=\"A&amp;B&lt;C\" />", root.ToFullString());
            Assert.Equal("A&B<C", root.GetAttributeValue("Include"));
        }

        [Fact]
        public void SetAttributeEscapesAQuoteInTheValue()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<r />").Root;

            root = root.SetAttribute("Condition", "'$(X)' == 'a\"b'");

            Assert.Equal("<r Condition=\"'$(X)' == 'a&quot;b'\" />", root.ToFullString());
            Assert.Equal("'$(X)' == 'a\"b'", root.GetAttributeValue("Condition"));
        }

        [Fact]
        public void SetAttributeKeepsTheQuoteCharacterTheAttributeWasWrittenWith()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<r a='old' />").Root;

            root = root.SetAttribute("a", "new\"quoted\"");

            Assert.Equal("<r a='new\"quoted\"' />", root.ToFullString());
        }

        [Fact]
        public void SetAttributeGivesAValuelessAttributeAValue()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root attr />").Root!;

            var text = root.SetAttribute("attr", "v").ToFullString();

            Assert.Equal("<root attr=\"v\" />", text);
            Assert.Equal("v", Parser.ParseText(text).Root!.GetAttributeValue("attr"));
        }

        [Fact]
        public void SetAttributeHandlesAnAttributeBuiltWithoutAValue()
        {
            XmlElementBaseSyntax element = XmlElement("a", XmlAttribute("x", null));

            Assert.Equal("<a x=\"v\" />", element.SetAttribute("x", "v").ToFullString());
        }

        [Fact]
        public void WithValueEscapesAndKeepsTheQuote()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root attr='old' />").Root!;
            XmlAttributeSyntax attribute = root.GetAttribute("attr")!;

            // ToFullString carries the attribute's own trailing trivia, the space before "/>".
            Assert.Equal("attr='a&amp;b' ", attribute.WithValue("a&b").ToFullString());
        }

        [Fact]
        public void WithValueGivesAParsedValuelessAttributeARealEquals()
        {
            // The parser answers "<root attr />" with a synthesized, zero-width "=" and string node,
            // so a null check is not what says whether there is a value to replace.
            XmlElementBaseSyntax root = Parser.ParseText("<root attr />").Root!;
            XmlAttributeSyntax attribute = root.GetAttribute("attr")!;

            Assert.Equal("attr=\"x&amp;y\" ", attribute.WithValue("x&y").ToFullString());

            var text = root.ReplaceNode(attribute, attribute.WithValue("x&y")).ToFullString();

            Assert.Equal("<root attr=\"x&amp;y\" />", text);
            Assert.Equal("x&y", Parser.ParseText(text).Root!.GetAttributeValue("attr"));
        }

        [Fact]
        public void SetAttributeKeepsAnAttributeOnItsOwnLine()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<a x=\"1\"\r\n   y=\"2\" />").Root!;

            Assert.Equal("<a x=\"1\"\r\n   y=\"9\" />", root.SetAttribute("y", "9").ToFullString());
        }

        [Theory]
        // A lone "=", and an unclosed quote: both are half-written, and both are completed rather
        // than added to. SetAttribute routes through WithValue so the two cannot answer differently.
        [InlineData("<a x= />", "<a x=\"v\" />")]
        [InlineData("<a x />", "<a x=\"v\" />")]
        [InlineData("<a x=\"1\" />", "<a x=\"v\" />")]
        [InlineData("<a x='1' />", "<a x='v' />")]
        public void SetAttributeAndWithValueAgree(string text, string expected)
        {
            XmlElementBaseSyntax root = Parser.ParseText(text).Root!;
            XmlAttributeSyntax attribute = root.GetAttribute("x")!;

            Assert.Equal(expected, root.SetAttribute("x", "v").ToFullString());
            Assert.Equal(expected, root.ReplaceNode(attribute, attribute.WithValue("v")).ToFullString());
        }

        [Theory]
        // A tag that closes straight after the quote does not grow a space it never had, and one
        // that has a separator keeps it - the new attribute takes over holding it.
        [InlineData("<a b=\"1\"/>", "<a b=\"1\" zz=\"v\"/>")]
        [InlineData("<a b=\"1\" />", "<a b=\"1\" zz=\"v\" />")]
        [InlineData("<a\r\n    b=\"1\"\r\n    c=\"2\" />", "<a\r\n    b=\"1\"\r\n    c=\"2\" zz=\"v\" />")]
        public void SetAttributeAddsWithoutInventingOrLosingTheSeparator(string text, string expected)
        {
            XmlElementBaseSyntax root = Parser.ParseText(text).Root!;

            Assert.Equal(expected, root.SetAttribute("zz", "v").ToFullString());
        }

        [Fact]
        public void SetAttributeOutputStillParses()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<r />").Root;

            var text = root.SetAttribute("Include", "A&B<C").ToFullString();

            Assert.Equal("A&B<C", Parser.ParseText(text).Root!.GetAttributeValue("Include"));
        }

        [Fact]
        public void XmlAttributeFactoryEscapesTheValue()
        {
            Assert.Equal("Include=\"A&amp;B\"", XmlAttribute("Include", "A&B").ToFullString());
        }

        [Fact]
        public void ElementTextContentIsEscaped()
        {
            Assert.Equal("<Note>a &lt; b &amp; c</Note>", XmlElement("Note", "a < b & c").ToFullString());
        }

        [Fact]
        public void WithTextReplacesContentWithEscapedText()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<r>old</r>").Root;

            Assert.Equal("<r>a &amp; b</r>", root.WithText("a & b").ToFullString());
        }

        [Fact]
        public void AttributeValueIsDecodedAndRawValueIsNot()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<r a=\"A&amp;B&#65;\" />").Root;
            XmlAttributeSyntax attribute = root.GetAttribute("a")!;

            Assert.Equal("A&BA", attribute.Value);
            Assert.Equal("A&amp;B&#65;", attribute.RawValue);
        }

        [Theory]
        // Every tab and line end in an attribute value is one space (XML 1.0 section 3.3.3), with
        // line ends normalized first so a CRLF counts once and "a\n\nb" keeps both of its.
        [InlineData("a\tb", "a b")]
        [InlineData("a\nb", "a b")]
        [InlineData("a\rb", "a b")]
        [InlineData("a\r\nb", "a b")]
        [InlineData("a\n\nb", "a  b")]
        [InlineData("a\r\n\nb", "a  b")]
        [InlineData("a\r\rb", "a  b")]
        // One a document means, rather than one it merely contains, is written as a reference.
        [InlineData("a&#x9;b", "a\tb")]
        [InlineData("a&#xA;b", "a\nb")]
        [InlineData("a&#xD;b", "a\rb")]
        // NEL and LINE SEPARATOR are line ends in XML 1.1 only, and this parser reads XML 1.0, so
        // they stay the characters the document wrote - as does the EN QUAD beside them, which a
        // typo in the old rule ate in place of the LINE SEPARATOR it was aimed at.
        [InlineData("a\u0085b", "a\u0085b")]
        [InlineData("a\u2028b", "a\u2028b")]
        [InlineData("a\u2000b", "a\u2000b")]
        [InlineData("a\r\u0085b", "a \u0085b")]
        public void AnAttributeValueNormalizesWhitespaceTheWayTheSpecSays(string written, string expected)
        {
            XmlElementBaseSyntax root = Parser.ParseText($"<r a=\"{written}\" />").Root!;

            Assert.Equal(expected, root.GetAttributeValue("a"));
        }

        [Fact]
        public void ElementValueIsDecodedAndRawValueIsNot()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<r><v>X&amp;Y</v></r>").Root;
            XmlElementBaseSyntax v = root.GetElement("v")!;

            Assert.Equal("X&Y", v.Value);
            Assert.Equal("X&amp;Y", v.RawValue);
        }

        [Fact]
        public void ElementValueUnwrapsCDataAndSkipsComments()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<r><v>a<!-- c -->b<![CDATA[<raw>]]></v></r>").Root;
            XmlElementBaseSyntax v = root.GetElement("v")!;

            Assert.Equal("ab<raw>", v.Value);
            Assert.Equal("a<!-- c -->b<![CDATA[<raw>]]>", v.RawValue);
        }

        [Fact]
        public void ElementValueIncludesTheTextOfNestedElements()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<r><v>a<b>c</b>d</v></r>").Root;

            Assert.Equal("acd", root.GetElement("v")!.Value);
        }

        [Fact]
        public void EmptyElementHasNoValue()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<r />").Root;

            Assert.Equal(string.Empty, root.Value);
            Assert.Equal(string.Empty, root.RawValue);
        }
    }
}
