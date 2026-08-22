using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Language.Xml.Collections;
using Xunit;
using static Microsoft.Language.Xml.SyntaxFactory;

namespace Microsoft.Language.Xml.Tests
{
    /// <summary>
    /// The properties every edit has to hold, checked against a corpus of documents rather than one
    /// example each. A test here says what must be true of *any* document; the corpus is what makes
    /// it a claim about the shapes real files come in - tabs, CRLF and LF and lone CR, minified,
    /// half-typed, welded, declared, prefixed - rather than about the one that happened to break.
    /// </summary>
    public class TestEditingInvariants
    {
        /// <summary>
        /// Documents that between them exercise every formatting decision the editing APIs make.
        /// Add a shape here and every invariant below covers it at once.
        /// </summary>
        public static readonly IReadOnlyList<string> Corpus = new[]
        {
            // -- well formed --

            // Line endings.
            "<r>\r\n  <a />\r\n</r>",
            "<r>\n  <a />\n</r>",
            "<r>\r  <a />\r</r>",
            // Indent units.
            "<r>\n    <a />\n</r>",
            "<r>\n\t<a />\n</r>",
            "<r>\n\t\t<a />\n</r>",
            // No layout to infer from.
            "<r></r>",
            "<r />",
            "<r><a /></r>",
            // The first child welded to the start tag, so it says nothing about indentation.
            "<r><a />\n  <b />\n</r>",
            // Children laid out inline.
            "<r><a /> <b /></r>",
            // A final newline past the root element.
            "<r><a /></r>\n",
            // A prologue, so the root element carries leading trivia of its own.
            "<?xml version=\"1.0\"?>\n<r>\n  <a />\n</r>",
            // Deep nesting, and an ancestor indented less than one unit per level.
            "<r>\n  <a>\n    <b>\n      <c />\n    </b>\n  </a>\n</r>",
            "<a>\n<b>\n<c>\n  <d />\n</c>\n</b>\n</a>",
            // Attributes, including a layout that puts them on separate lines.
            "<r a=\"1\" b=\"2\">\n  <x />\n</r>",
            "<r\n    a=\"1\"\n    b=\"2\">\n  <x />\n</r>",
            "<r a='1' />",
            // Whitespace around "=" is legal, and the space belongs to the name rather than to the
            // attribute, so it is not the separator to the next one.
            "<r x =\"1\" />",
            "<r x = \"1\" y=\"2\" />",
            // An unescaped ">" is legal in an attribute value and says nothing about the tag.
            "<r x=\"a>b\" y=\"1\" />",
            // Whitespace inside an attribute value, which XML normalizes on the way out.
            "<r a=\"line\nbreak\tand tab\" />",
            // Content that is not elements.
            "<r>text</r>",
            "<r>\n  <!-- comment -->\n  <a />\n</r>",
            "<r><![CDATA[<raw>]]></r>",
            "<r>\n  <?pi data?>\n  <a />\n</r>",
            // Namespaces.
            "<p:r xmlns:p=\"urn:x\">\n  <p:a />\n</p:r>",
        };

        /// <summary>
        /// The shapes a document takes while it is being typed. A parser that gives up on these is
        /// no use in an editor, so they get the invariants that still make sense - the tree round
        /// trips, the edits do not throw, the output parses - but not the ones about meaning, since
        /// the input has none to preserve.
        /// </summary>
        public static readonly IReadOnlyList<string> MalformedCorpus = new[]
        {
            "<r attr />",
            "<r attr= />",
            "<r a=\"unclosed />",
            "<r>\n  <a>\n</r>",
            "<r>",
            "<r a=\"1\" a=\"2\" />",
            "<r></q>",
            // Half-written, and not last: the unclosed value eats the rest of the tag, so what the
            // parser reports after it is text from outside the element.
            "<r x= b=\"1\" />",
            "<r a=\"unclosed b=\"2\" />",
            "<r x= b=\"1\"></r>",
            // A document with no element at all is deliberately absent: an edit has nowhere to put
            // anything, so there is no claim to make beyond "does not throw", which the parsing
            // invariants already cover.
        };

        public static TheoryData<string> Documents => ToData(Corpus);

        public static TheoryData<string> AllDocuments => ToData(Corpus.Concat(MalformedCorpus));

        private static TheoryData<string> ToData(IEnumerable<string> documents)
        {
            var data = new TheoryData<string>();

            foreach (var document in documents)
            {
                data.Add(document);
            }

            return data;
        }

        /// <summary>
        /// Strings that between them exercise every branch of the escaping and decoding rules.
        /// </summary>
        public static TheoryData<string> Values
        {
            get
            {
                var data = new TheoryData<string>();

                foreach (var value in new[]
                {
                    "",
                    "plain",
                    "A&B<C>D",
                    "]]>",
                    "quotes \" and '",
                    "&amp; already looks escaped",
                    "&nbsp;",
                    "&#65;",
                    "tab\there",
                    "line\nbreak",
                    "carriage\rreturn",
                    "  leading and trailing  ",
                    // Whitespace with nothing else beside it: the scanner keeps it as trivia on
                    // the end tag rather than as content, so it is the one value an element can
                    // hold without holding any content at all.
                    " ",
                    "\t",
                    "\n",
                    "unicode é中\U0001F600",
                })
                {
                    data.Add(value);
                }

                return data;
            }
        }

        // ---------------------------------------------------------------- parsing

        [Theory]
        [MemberData(nameof(AllDocuments))]
        public void ParsingIsLossless(string text)
        {
            Assert.Equal(text, Parser.ParseText(text).ToFullString());
        }

        [Theory]
        [MemberData(nameof(AllDocuments))]
        public void EveryEditProducesTextThatParsesBackToItself(string text)
        {
            // The weakest claim there is, and the one that has to hold even for input that means
            // nothing: whatever an edit writes, the parser reads back unchanged.
            XmlElementBaseSyntax? root = Parser.ParseText(text).Root;

            if (root is null)
            {
                return;
            }

            foreach (XmlElementBaseSyntax edited in new[]
            {
                root.AddChild(XmlEmptyElement("added")),
                root.AddChild(XmlEmptyElement("added"), indent: false),
                root.InsertChild(XmlEmptyElement("added"), 0),
                root.SetAttribute("added", "a&b"),
                root.WithText("a & b"),
                root.GetOrAddElement("one/two", out _),
            })
            {
                var written = edited.ToFullString();

                Assert.Equal(written, Parser.ParseText(written).ToFullString());
            }
        }

        [Theory]
        [MemberData(nameof(Documents))]
        public void EveryEditOnAWellFormedDocumentLeavesItWellFormed(string text)
        {
            // Reparsing with this parser cannot answer the question: it is error-tolerant by
            // design and reads anything at all back byte-identical, so an edit that wrote broken
            // markup would pass. A stricter reader has to be the judge.
            XmlElementBaseSyntax root = Root(text);

            foreach (XmlElementBaseSyntax edited in new[]
            {
                root.AddChild(XmlEmptyElement("added")),
                root.AddChild(XmlEmptyElement("added"), indent: false),
                root.InsertChild(XmlEmptyElement("added"), 0),
                root.SetAttribute("added", "a&b<c>d]]>e\"f'g"),
                root.WithText("a & b < c > d ]]> e"),
                root.GetOrAddElement("one/two", out _),
            })
            {
                var written = edited.ToFullString();

                // Throws XmlException if the edit wrote anything XML does not allow.
                XDocument.Parse(written, LoadOptions.PreserveWhitespace);
            }
        }

        [Theory]
        [MemberData(nameof(AllDocuments))]
        public void AnEditIsReadableAgainEvenOnHalfWrittenInput(string text)
        {
            // Input with no meaning to preserve is still no licence to drop the caller's: an
            // attribute set on a document that is mid-keystroke has to survive the round trip,
            // rather than being swallowed by the half-written attribute in front of it.
            XmlElementBaseSyntax? root = Parser.ParseText(text).Root;

            if (root is null)
            {
                return;
            }

            var before = root.Attributes.Select(x => (x.Name, x.Value)).ToList();

            var withAttribute = root.SetAttribute("added", "v").ToFullString();
            XmlElementBaseSyntax after = Parser.ParseText(withAttribute).Root!;

            Assert.Equal("v", after.GetAttributeValue("added"));

            // And nothing that could be read before has stopped reading: an edit that splices into
            // a run of half-written text leaves the new attribute findable while quietly cutting
            // the old ones in half.
            foreach ((var name, var value) in before)
            {
                Assert.Contains(
                    after.Attributes,
                    x => x.Name == name && x.Value == value);
            }

            var withChild = root.AddChild(XmlEmptyElement("added")).ToFullString();

            Assert.Single(Parser.ParseText(withChild).Descendants("added"));
        }

        [Theory]
        [MemberData(nameof(AllDocuments))]
        public void SetAttributeStaysIdempotentEvenOnHalfWrittenInput(string text)
        {
            XmlElementBaseSyntax? root = Parser.ParseText(text).Root;

            if (root is null)
            {
                return;
            }

            var once = root.SetAttribute("added", "v").ToFullString();

            Assert.Equal(once, Root(once).SetAttribute("added", "v").ToFullString());
        }

        [Theory]
        [InlineData("xmlns:p", "urn:x")]
        [InlineData("p:name", "v")]
        public void APrefixedAttributeIsFoundAgainRatherThanDuplicated(string name, string value)
        {
            var once = Root("<r />").SetAttribute(name, value).ToFullString();

            Assert.Equal($"<r {name}=\"{value}\" />", once);
            Assert.Equal(once, Root(once).SetAttribute(name, value).ToFullString());
        }

        [Theory]
        [InlineData("")]
        [InlineData(":name")]
        [InlineData("prefix:")]
        [InlineData("a:b:c")]
        [InlineData("x y")]
        [InlineData("x>")]
        [InlineData("x=")]
        [InlineData("x\"y")]
        [InlineData("1abc")]
        public void AnAttributeNameTheDocumentCouldNotReadBackIsRejected(string name)
        {
            // The value is escaped on the way in, so any string is safe there. A name is not:
            // writing one of these produces a document that parses as something else.
            Assert.Throws<ArgumentException>(() => Root("<r />").SetAttribute(name, "v"));
        }

        [Theory]
        [InlineData("name")]
        [InlineData("_name")]
        [InlineData("name-with-dashes")]
        [InlineData("name.with.dots")]
        [InlineData("name123")]
        [InlineData("p:name")]
        // XML names run to most of Unicode. A rule written as "letters and digits" rejects an
        // ordinary Devanagari or Thai name, a combining mark, the same Latin name in a different
        // normal form, and anything outside the basic plane.
        [InlineData("क्रम")]
        [InlineData("น้ำ")]
        [InlineData("état")]
        [InlineData("a·b")]
        [InlineData("\U00012000")]
        public void AnAttributeNameTheDocumentCanReadBackIsAccepted(string name)
        {
            var text = Root("<r />").SetAttribute(name, "v").ToFullString();

            Assert.Equal($"<r {name}=\"v\" />", text);

            // Actually read it back. Asserting only the text written cannot tell an accepted name
            // from one the scanner cuts short - which is the whole question being asked here.
            var parts = name.Split(':');

            Assert.Equal("v", Root(text).GetAttributeValue(parts.Last(), parts.Length > 1 ? parts[0] : null));
            Assert.Equal(text, Root(text).SetAttribute(name, "v").ToFullString());
        }

        [Theory]
        // Characters that end a name where it stands, so what is written is not the name given.
        [InlineData("x;y")]
        [InlineData("x,y")]
        [InlineData("x(y")]
        [InlineData("x)y")]
        [InlineData("x}y")]
        [InlineData("xy")]
        [InlineData("x\0y")]
        [InlineData("x￿y")]
        // A well-formed pair, but above U+EFFFF, where XML stops giving names. The scanner
        // refuses it, so writing it produces a document nothing reads back.
        [InlineData("x\U000F1234y")]
        [InlineData("\U000F1234")]
        [InlineData("x\U0010FFFFy")]
        // Not a name start, whatever they may be later on.
        [InlineData("#y")]
        [InlineData("[y")]
        [InlineData("‘y")]
        public void AnAttributeNameTheScannerWouldCutShortIsRejected(string name)
        {
            Assert.Throws<ArgumentException>(() => Root("<r />").SetAttribute(name, "v"));
        }

        [Fact]
        public void AHalfOfASurrogatePairIsNotACharacterANameCanHold()
        {
            // Not rows on the theory above: a high and a low surrogate on their own serialize to
            // the same unprintable test name, and xUnit silently drops the second as a duplicate,
            // so only one of the two would ever run.
            Assert.Throws<ArgumentException>(() => Root("<r />").SetAttribute("x\ud800y", "v"));
            Assert.Throws<ArgumentException>(() => Root("<r />").SetAttribute("x\udc00y", "v"));
        }

        [Theory]
        // The bare name holds its own separator, so the new attribute must not add a second one -
        // and must still leave one in front of the closing token.
        [InlineData("<a x />", "<a x y=\"1\" />")]
        [InlineData("<a x/>", "<a x y=\"1\"/>")]
        public void AddingAnAttributeAfterABareNameKeepsOneSeparator(string text, string expected)
        {
            Assert.Equal(expected, Root(text).SetAttribute("y", "1").ToFullString());
        }

        [Theory]
        // Whitespace around "=" is legal. The space in front of it belongs to the name, not to the
        // attribute, so taking it for the separator welds the new attribute to the closing quote.
        [InlineData("<a x =\"1\" />", "<a x =\"1\" z=\"2\" />")]
        [InlineData("<a x = \"1\"/>", "<a x = \"1\" z=\"2\"/>")]
        // A ">" inside a value is legal and says nothing about the tag, so nothing is reordered.
        [InlineData("<a x=\"a>b\" y=\"1\" />", "<a x=\"a>b\" y=\"1\" z=\"2\" />")]
        public void AWellFormedTagKeepsItsAttributesWhereTheyWere(string text, string expected)
        {
            Assert.Equal(expected, Root(text).SetAttribute("z", "2").ToFullString());
        }

        [Theory]
        // The value with no closing quote eats the rest of the tag, so what the parser reports
        // after it is one run of stolen text with no position inside it to write at. The new
        // attribute goes right after the element name and the run is left exactly as it was.
        [InlineData("<r x= b=\"1\" />", "<r q=\"v\" x= b=\"1\" />")]
        [InlineData("<r x= />", "<r q=\"v\" x= />")]
        [InlineData("<r a=\"unclosed b=\"2\" />", "<r q=\"v\" a=\"unclosed b=\"2\" />")]
        public void AHalfWrittenTagIsWrittenInFrontOfRatherThanInto(string text, string expected)
        {
            Assert.Equal(expected, Root(text).SetAttribute("q", "v").ToFullString());
        }

        // ---------------------------------------------------------------- AddChild

        [Theory]
        [MemberData(nameof(Documents))]
        public void AddChildProducesADocumentThatStillParses(string text)
        {
            XmlDocumentSyntax edited = Edit(text, root => root.AddChild(XmlEmptyElement("added")));

            Assert.NotNull(edited.Root!.GetElement("added"));
        }

        [Theory]
        [MemberData(nameof(Documents))]
        public void AddChildDoesNotIntroduceAForeignLineEnding(string text)
        {
            AssertLineEndingsAgree(text, Edit(text, root => root.AddChild(XmlEmptyElement("added"))).ToFullString());
        }

        [Theory]
        [MemberData(nameof(Documents))]
        public void AddChildPlacesEveryChildTheSameWay(string text)
        {
            // Whatever the layout decision is for this document, chaining has to keep making it -
            // the node an edit hands back is detached, which is where indent arithmetic goes wrong.
            XmlElementBaseSyntax root = Root(text);

            XmlElementSyntax twice = root
                .AddChild(XmlEmptyElement("one"))
                .AddChild(XmlEmptyElement("two"));

            XmlElementBaseSyntax one = twice.GetElement("one")!;
            XmlElementBaseSyntax two = twice.GetElement("two")!;

            Assert.Equal(one.GetLeadingTrivia().ToFullString(), two.GetLeadingTrivia().ToFullString());
        }

        [Theory]
        [MemberData(nameof(Documents))]
        public void AddChildWithoutIndentingAddsNoFormatting(string text)
        {
            XmlElementSyntax edited = Root(text).AddChild(XmlEmptyElement("added"), indent: false);

            Assert.False(edited.GetElement("added")!.HasLeadingTrivia);
        }

        [Theory]
        [MemberData(nameof(Documents))]
        public void InsertChildAtTheFrontStillParses(string text)
        {
            XmlDocumentSyntax edited = Edit(text, root => root.InsertChild(XmlEmptyElement("added"), 0));

            Assert.NotNull(edited.Root!.GetElement("added"));
        }

        // ---------------------------------------------------------------- attributes

        [Theory]
        [MemberData(nameof(Documents))]
        public void SetAttributeReadsBackWhatWasWritten(string text)
        {
            const string Value = "a&b<c>\"d\"'e'";

            XmlDocumentSyntax edited = Edit(text, root => root.SetAttribute("added", Value));

            Assert.Equal(Value, edited.Root!.GetAttributeValue("added"));
        }

        [Theory]
        [MemberData(nameof(Documents))]
        public void SetAttributeLeavesTheOtherAttributesAlone(string text)
        {
            var before = Root(text).Attributes.Select(x => (x.Name, x.Value)).ToList();

            var after = Edit(text, x => x.SetAttribute("added", "v")).Root!
                .Attributes
                .Select(x => (x.Name, x.Value))
                .ToList();

            // The new one is appended, so everything that was there stays where it was.
            Assert.Equal(before, after.Take(before.Count));
            Assert.Equal(("added", "v"), after.Last());
        }

        [Theory]
        [MemberData(nameof(Documents))]
        public void SetAttributeIsIdempotent(string text)
        {
            XmlElementBaseSyntax once = Root(text).SetAttribute("added", "v");

            Assert.Equal(once.ToFullString(), once.SetAttribute("added", "v").ToFullString());
        }

        [Theory]
        [MemberData(nameof(Documents))]
        public void SetAttributeAndWithValueAgreeOnEveryDocument(string text)
        {
            XmlElementBaseSyntax root = Root(text).SetAttribute("added", "first");
            XmlAttributeSyntax attribute = root.GetAttribute("added")!;

            Assert.Equal(
                root.SetAttribute("added", "second").ToFullString(),
                root.ReplaceNode(attribute, attribute.WithValue("second")).ToFullString());
        }

        [Theory]
        [MemberData(nameof(Values))]
        public void AnAttributeHoldsAnyValueVerbatim(string value)
        {
            foreach (var quoted in new[] { "<r />", "<r a='x' />", "<r a=\"x\" />", "<r a />" })
            {
                XmlElementBaseSyntax root = Root(quoted).SetAttribute("a", value);

                Assert.Equal(value, root.GetAttributeValue("a"));
                Assert.Equal(value, Reparse(root).GetAttributeValue("a"));
            }
        }

        // ---------------------------------------------------------------- text

        [Theory]
        [MemberData(nameof(Values))]
        public void AnElementHoldsAnyTextVerbatim(string value)
        {
            XmlElementBaseSyntax root = Root("<r>old</r>").WithText(value);

            Assert.Equal(value, root.Value);
            Assert.Equal(value, Reparse(root).Value);
        }

        [Theory]
        [MemberData(nameof(Documents))]
        public void WhatAddElementHandsBackIsPartOfTheTreeItReturns(string text)
        {
            // A node fetched out of an intermediate tree looks right and is not there: every edit
            // made through it lands on an orphan and is dropped without a word.
            XmlElementBaseSyntax root = Root(text).AddElement("one/two", out XmlElementBaseSyntax added);

            Assert.Contains("k=\"1\"", root.ReplaceNode(added, added.SetAttribute("k", "1")).ToFullString());

            SyntaxNode top = added;

            while (top.Parent is { } parent)
            {
                top = parent;
            }

            Assert.Same(root, top);
        }

        [Theory]
        [MemberData(nameof(Values))]
        public void AnElementHoldsAnyTextVerbatimNestedToo(string value)
        {
            // The same text, one level down. A parent that walks its children has to reach the
            // whitespace they hold as trivia, or it disagrees with what its own child says.
            XmlElementBaseSyntax root = Root("<r><c>old</c></r>");
            root = root.ReplaceNode(root.GetElement("c")!, root.GetElement("c")!.WithText(value));

            Assert.Equal(value, root.GetElement("c")!.Value);
            Assert.Equal(value, root.Value);

            XmlElementBaseSyntax reparsed = Reparse(root);

            Assert.Equal(value, reparsed.GetElement("c")!.Value);
            Assert.Equal(value, reparsed.Value);
        }

        [Theory]
        [InlineData("<r>\n</r>", "t")]
        [InlineData("<r>\n  </r>", " ")]
        [InlineData("<r>\n  <a />\n</r>", "t")]
        [InlineData("<r>old</r>", "t")]
        public void WithTextReplacesTheWhitespaceBesideTheEndTagToo(string text, string value)
        {
            // Whitespace running up to the end tag is content as far as Value is concerned, so
            // leaving it behind makes set-then-get answer with more than was set.
            XmlElementBaseSyntax root = Root(text).WithText(value);

            Assert.Equal(value, root.Value);
            Assert.Equal(value, Reparse(root).Value);
        }

        [Theory]
        [InlineData("<a><b> </b></a>", " ")]
        [InlineData("<a>x<b> </b>y</a>", "x y")]
        [InlineData("<a><b/>\n</a>", "\n")]
        [InlineData("<a>\n  <b>x</b>\n</a>", "\n  x\n")]
        [InlineData("<a> <!-- c --> </a>", "  ")]
        [InlineData("<a> <![CDATA[x]]></a>", " x")]
        [InlineData("<a><b/>\n<![CDATA[x]]>\n</a>", "\nx\n")]
        // A literal CRLF or lone CR in the document stands for one LF (XML 1.0 section 2.11),
        // wherever it is - text, whitespace between tags, or inside a CDATA section. A carriage
        // return the document actually means is written "&#xD;", and that one survives.
        [InlineData("<a>x\ry</a>", "x\ny")]
        [InlineData("<a>x\r\ny</a>", "x\ny")]
        [InlineData("<a>x&#xD;y</a>", "x\ry")]
        [InlineData("<a><![CDATA[x\r\ny]]></a>", "x\ny")]
        [InlineData("<a>\r\n  <b>x</b>\r\n</a>", "\n  x\n")]
        public void WhitespaceBetweenTagsIsPartOfWhatTheElementSays(string text, string expected)
        {
            // What XDocument answers for the same document, loaded with PreserveWhitespace. The
            // scanner keeps whitespace running up to a tag as that tag's trivia, so it is
            // reachable but not in Content.
            Assert.Equal(expected, Root(text).Value);
        }

        [Theory]
        [MemberData(nameof(Documents))]
        public void RawValueIsExactlyTheTextContentSpanPointsAt(string text)
        {
            XmlDocumentSyntax document = Parser.ParseText(text);
            var full = document.ToFullString();

            foreach (XmlElementBaseSyntax element in document.Descendants())
            {
                TextSpan span = element.ContentSpan;

                Assert.Equal(full.Substring(span.Start, span.Length), element.RawValue);
            }
        }

        [Fact]
        public void ADeeplyNestedDocumentIsAnAnswerRatherThanACrash()
        {
            // A parser that tolerates whatever a buffer holds cannot answer one pathological
            // paste by taking the process down with it - a stack overflow is not catchable.
            const int Depth = 50000;

            var text = string.Concat(Enumerable.Repeat("<a>", Depth))
                + "x"
                + string.Concat(Enumerable.Repeat("</a>", Depth));

            XmlElementBaseSyntax root = Root(text);

            Assert.Equal("x", root.Value);
            Assert.Equal("x", root.Descendants().Last().Value);
            Assert.Equal(text, root.ToFullString());
        }

        [Fact]
        public void TextArrivingAsTwoChildrenStillCannotCloseACDataSection()
        {
            // Each string is escaped on its own, and neither can see the "]]>" the two of them
            // make once they are next to each other.
            XmlElementBaseSyntax element = XmlElement("a", "x]]", ">");

            Assert.Equal("<a>x]]&gt;</a>", element.ToFullString());
            Assert.Equal("x]]>", element.Value);
            Assert.Equal("x]]>", Reparse(element).Value);

            // The same break one character over, and one character at a time - a "]]>" can be
            // split three ways, so looking one child back is not enough.
            Assert.Equal("<a>x]]&gt;y</a>", XmlElement("a", "x]", "]>y").ToFullString());
            Assert.Equal("<a>]]&gt;</a>", XmlElement("a", "]", "]", ">").ToFullString());
            Assert.Equal("<a>x]]&gt;</a>", XmlElement("a", "x", "]", "]", ">").ToFullString());
            Assert.Equal("<a>]]&gt;</a>", XmlElement("a", new object[] { new object[] { "]" }, "]", ">" }).ToFullString());

            // A child that ends in markup breaks the run, so nothing needs escaping.
            var comment = (XmlNodeSyntax)Parser.ParseText("<a><!--c--></a>").Root!.Content[0];

            Assert.Equal("<a>x]]<!--c-->></a>", XmlElement("a", "x]]", comment, ">").ToFullString());

            // A node ends in its own text, so what it leaves behind counts against the string that
            // follows it. The other direction is not the same question: a node is markup the
            // caller built and handed over, and rewriting it is not on offer - the escaping
            // promise is about the strings, which are the arguments the caller cannot escape
            // themselves. So XmlElement("a", "]]", textNode(">")) writes "]]>" and means to.
            XmlNodeSyntax brackets = XmlText(List<SyntaxNode>(XmlTextLiteralToken("]]", null, null)));

            Assert.Equal("<a>]]&gt;</a>", XmlElement("a", brackets, ">").ToFullString());

            // And a one-character node does not wipe out the bracket in front of it.
            XmlNodeSyntax bracket = XmlText(List<SyntaxNode>(XmlTextLiteralToken("]", null, null)));

            Assert.Equal("<a>]]&gt;</a>", XmlElement("a", "]", bracket, ">").ToFullString());
            Assert.Equal("<a>]]&gt;</a>", XmlElement("a", bracket, "]", ">").ToFullString());
        }

        [Theory]
        [MemberData(nameof(Values))]
        public void EncodingThenDecodingIsTheIdentity(string value)
        {
            Assert.Equal(value, XmlEscaping.Decode(XmlEscaping.EncodeText(value)));
            Assert.Equal(value, XmlEscaping.Decode(XmlEscaping.EncodeAttributeValue(value, '"')));
            Assert.Equal(value, XmlEscaping.Decode(XmlEscaping.EncodeAttributeValue(value, '\'')));
        }

        // ---------------------------------------------------------------- formatting inference

        [Theory]
        [MemberData(nameof(AllDocuments))]
        public void GetNewLineAnswersWithTheLineEndingTheDocumentUses(string text)
        {
            XmlElementBaseSyntax? root = Parser.ParseText(text).Root;

            if (root is null)
            {
                return;
            }

            // Worked out from the text independently, so a regression that answers with the wrong
            // flavour - "\n" for a CRLF file - fails here rather than passing a substring check.
            Assert.Equal(FirstLineEnding(text) ?? "\r\n", root.GetNewLine());
        }

        [Theory]
        [InlineData("<r>\r\n  <a />\r\n</r>", "\r\n")]
        [InlineData("<r>\n  <a />\n</r>", "\n")]
        [InlineData("<r>\r  <a />\r</r>", "\r")]
        // The only line break past where a bounded forward scan reaches: on the end tag, and past
        // the root element entirely.
        [InlineData("<r><a /></r>\n", "\n")]
        [InlineData("<r><a />\n</r>", "\n")]
        // Inside a token rather than in trivia.
        [InlineData("<r>text\nmore</r>", "\n")]
        // Nothing to go on.
        [InlineData("<r><a /></r>", "\r\n")]
        public void GetNewLineReadsTheDocumentsOwnLineEnding(string text, string expected)
        {
            Assert.Equal(expected, Root(text).GetNewLine());
        }

        [Fact]
        public void GetNewLineReadsPastAMinifiedPrefix()
        {
            // Long enough that a bounded forward scan gives up before reaching either break.
            var minified = string.Concat(Enumerable.Repeat("<a x=\"1\"/>", 3000));

            Assert.Equal("\n", Root("<r>" + minified + "\n</r>").GetNewLine());
            Assert.Equal("\n", Root("<r>" + minified + "</r>\n").GetNewLine());
        }

        [Theory]
        [InlineData("<r>\n  <a />\n</r>", "  ")]
        [InlineData("<r>\n    <a />\n</r>", "    ")]
        [InlineData("<r>\n\t<a />\n</r>", "\t")]
        // The first child is welded to the start tag, so it says nothing; the second one does.
        [InlineData("<r><a />\n\t<b />\n</r>", "\t")]
        // A separator between inline children is not an indent, however wide it is.
        [InlineData("<r><a /> <b /></r>", "  ")]
        [InlineData("<r></r>", "  ")]
        public void GetIndentUnitReadsTheDocumentsOwnLayout(string text, string expected)
        {
            Assert.Equal(expected, Root(text).GetIndentUnit());
        }

        [Theory]
        [MemberData(nameof(AllDocuments))]
        public void GetIndentUnitAnswersWithWhitespace(string text)
        {
            XmlElementBaseSyntax? root = Parser.ParseText(text).Root;

            if (root is null)
            {
                return;
            }

            var unit = root.GetIndentUnit();

            Assert.NotEmpty(unit);
            Assert.All(unit, c => Assert.True(c == ' ' || c == '\t', $"'{unit}' is not an indent."));
        }

        // ---------------------------------------------------------------- paths

        [Theory]
        [MemberData(nameof(Documents))]
        public void GetOrAddElementByPathIsIdempotent(string text)
        {
            XmlElementBaseSyntax once = Root(text).GetOrAddElement("one/two/three", out _);

            Assert.Equal(once.ToFullString(), once.GetOrAddElement("one/two/three", out _).ToFullString());
            Assert.Single(once.GetElementsByPath("one/two/three"));
        }

        [Theory]
        [MemberData(nameof(Documents))]
        public void GetOrAddElementAcceptsALeadingSlash(string text)
        {
            XmlElementBaseSyntax root = Root(text);

            Assert.Equal(
                root.GetOrAddElement("one/two", out _).ToFullString(),
                root.GetOrAddElement("/one/two", out _).ToFullString());
        }

        [Theory]
        [InlineData("")]
        [InlineData("/")]
        [InlineData("a/")]
        [InlineData("a//b")]
        [InlineData("//a")]
        public void APathThatWouldNameNothingIsRejected(string path)
        {
            XmlElementBaseSyntax root = Root("<r />");

            Assert.Throws<ArgumentException>(() => root.GetOrAddElement(path, out _));
            Assert.Throws<ArgumentException>(() => root.GetElementsByPath(path).Count());
        }

        [Theory]
        [InlineData("a b")]
        [InlineData("a<c")]
        [InlineData("a>")]
        [InlineData("a\"b")]
        [InlineData("a=")]
        [InlineData("1x")]
        [InlineData(" a")]
        [InlineData("a ")]
        [InlineData("x;y")]
        [InlineData("a:b:c")]
        [InlineData("ok/a b")]
        [InlineData("a b/ok")]
        public void APathSegmentTheDocumentCouldNotReadBackIsRejected(string path)
        {
            // Creating writes the segment into the document. "a b" comes back as an element "a"
            // with an attribute "b", so the next get-or-add does not find it and adds another.
            XmlElementBaseSyntax root = Root("<r />");

            Assert.Throws<ArgumentException>(() => root.GetOrAddElement(path, out _));
            Assert.Throws<ArgumentException>(() => root.AddElement(path, out _));
        }

        [Theory]
        [InlineData("a b")]
        [InlineData("1x")]
        [InlineData("a:b:c")]
        public void APathThatOnlySelectsTakesAnyName(string path)
        {
            // Selection writes nothing, so a segment no element can be named simply matches none.
            Assert.Empty(Root("<r />").GetElementsByPath(path));
        }

        [Theory]
        [InlineData("p:a/q:b")]
        [InlineData("_a/a-b/a.b/a1")]
        [InlineData("क्रम/น้ำ")]
        public void APathSegmentTheDocumentCanReadBackIsAccepted(string path)
        {
            XmlElementBaseSyntax once = Root("<r />").GetOrAddElement(path, out _);

            Assert.Single(Root(once.ToFullString()).GetElementsByPath(path));
            Assert.Equal(once.ToFullString(), Root(once.ToFullString()).GetOrAddElement(path, out _).ToFullString());

            // On the tree the call returned, not on a reparse of it. A name written as one flat
            // token gives the right text and the wrong tree, which only a reparse repairs.
            Assert.Single(once.GetElementsByPath(path));
        }

        [Fact]
        public void ACreatedElementKnowsItsOwnPrefix()
        {
            XmlElementBaseSyntax root = Root("<r />").GetOrAddElement("p:a", out XmlElementBaseSyntax added);

            Assert.Equal("a", added.NameNode.LocalName);
            Assert.Equal("p", added.NameNode.Prefix);
            Assert.NotNull(root.GetElement("a", "p"));
            Assert.Equal("<r>\r\n  <p:a />\r\n</r>", root.ToFullString());
        }

        // ---------------------------------------------------------------- enumerators

        [Theory]
        [MemberData(nameof(Documents))]
        public void EveryEnumeratorYieldsTheSameSequenceTwice(string text)
        {
            XmlElementBaseSyntax root = Root(text);

            AssertRepeatable(root.Elements);
            AssertRepeatable(root.GetElements("a"));
            AssertRepeatable(root.GetElementsByLocalName("a"));
            AssertRepeatable(root.Descendants());
            AssertRepeatable(root.GetElementsByPath("a"));
            AssertRepeatable(root.Attributes.Select(x => (SyntaxNode)x));
        }

        [Theory]
        [MemberData(nameof(Documents))]
        public void EveryEnumeratorHandsOutAWholeSequenceAfterBeingAdvanced(string text)
        {
            // A struct enumerator that hands out itself gives a foreach the tail of its own walk,
            // so whether the caller happened to touch it first changes what the loop sees.
            XmlElementBaseSyntax root = Root(text);

            XmlElementEnumerator elements = root.Elements;
            XmlNamedElementEnumerator named = root.GetElements("a");
            XmlDescendantElementEnumerator descendants = root.Descendants();
            XmlAttributeNodeEnumerator attributes = root.Attributes;
            SyntaxNodeEnumerator content = root.ChildNodes;

            if (elements.Count() > 0)
            {
                var expected = elements.Count();
                Assert.True(elements.MoveNext());
                Assert.Equal(expected, elements.Count());
            }

            if (named.Count() > 0)
            {
                var expected = named.Count();
                Assert.True(named.MoveNext());
                Assert.Equal(expected, named.Count());
            }

            if (descendants.Count() > 0)
            {
                var expected = descendants.Count();
                Assert.True(descendants.MoveNext());
                Assert.Equal(expected, descendants.Count());
            }

            if (attributes.Count > 0)
            {
                Assert.True(attributes.MoveNext());
                Assert.Equal(attributes.Count, attributes.Count());
            }

            if (content.Count() > 0)
            {
                var expected = content.Count();
                Assert.True(content.MoveNext());
                Assert.Equal(expected, content.Count());
            }
        }

        [Theory]
        [MemberData(nameof(Documents))]
        public void DescendantsFindsEverythingTheTreeHolds(string text)
        {
            XmlElementBaseSyntax root = Root(text);

            var walked = root.DescendantNodes()
                .OfType<XmlElementBaseSyntax>()
                .Count();

            Assert.Equal(walked, root.Descendants().Count());
        }

        [Fact]
        public void FirstMeansTheFirstEvenOnAnAdvancedEnumerator()
        {
            // Copying the position rather than starting over turns "first" into "next".
            XmlElementBaseSyntax root = Root("<r a=\"1\" b=\"2\">\n  <x />\n  <y />\n</r>");

            XmlAttributeNodeEnumerator attributes = root.Attributes;
            Assert.True(attributes.MoveNext());
            Assert.Equal("a", attributes.FirstOrDefault()!.Name);
            Assert.Equal("a", attributes.First().Name);

            XmlElementEnumerator elements = root.Elements;
            Assert.True(elements.MoveNext());
            Assert.Equal("x", elements.FirstOrDefault()!.Name);
            Assert.Equal("x", elements.First().Name);

            // And the caller's own position is still where they left it.
            Assert.True(attributes.MoveNext());
            Assert.Equal("b", attributes.Current.Name);
            Assert.True(elements.MoveNext());
            Assert.Equal("y", elements.Current.Name);
        }

        // ---------------------------------------------------------------- spans

        [Theory]
        [MemberData(nameof(Documents))]
        public void EverySpanPointsAtTheTextItClaims(string text)
        {
            XmlDocumentSyntax document = Parser.ParseText(text);
            var full = document.ToFullString();

            foreach (XmlElementBaseSyntax element in document.Descendants())
            {
                AssertInBounds(element.ContentSpan, full);

                foreach (XmlAttributeSyntax attribute in element.Attributes)
                {
                    AssertInBounds(attribute.ValueSpan, full);

                    // The text between the quotes, not RawValue: RawValue is normalized, so a
                    // value written across two lines reads back with a space where the break was.
                    Assert.Equal(
                        attribute.ValueNode?.TextTokens.ToFullString() ?? string.Empty,
                        full.Substring(attribute.ValueSpan.Start, attribute.ValueSpan.Length));
                }
            }
        }

        [Theory]
        [MemberData(nameof(AllDocuments))]
        public void FindNodeAnswersEverywhereInTheBuffer(string text)
        {
            XmlDocumentSyntax document = Parser.ParseText(text);

            for (var position = 0; position <= text.Length; position++)
            {
                Assert.NotNull(SyntaxLocator.FindNode(document, position));
            }
        }

        // ---------------------------------------------------------------- helpers

        private static string? FirstLineEnding(string text)
        {
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    return "\n";
                }

                if (text[i] == '\r')
                {
                    return i + 1 < text.Length && text[i + 1] == '\n' ? "\r\n" : "\r";
                }
            }

            return null;
        }

        private static XmlElementBaseSyntax Root(string text)
        {
            return Parser.ParseText(text).Root!;
        }

        private static XmlElementBaseSyntax Reparse(XmlElementBaseSyntax element)
        {
            return Root(element.ToFullString());
        }

        /// <summary>
        /// Applies an edit to the document's root and reparses the result, so that every invariant
        /// is a claim about text a parser accepts and not only about the tree in hand.
        /// </summary>
        private static XmlDocumentSyntax Edit(string text, Func<XmlElementBaseSyntax, XmlElementBaseSyntax> edit)
        {
            var edited = edit(Root(text)).ToFullString();
            XmlDocumentSyntax document = Parser.ParseText(edited);

            Assert.Equal(edited, document.ToFullString());

            return document;
        }

        /// <summary>
        /// An edit may add line breaks - indenting is the point - but never one written differently
        /// from the ones the document already has. A document with no line break at all says
        /// nothing to disagree with.
        /// </summary>
        private static void AssertLineEndingsAgree(string before, string after)
        {
            if (before.IndexOfAny(new[] { '\r', '\n' }) < 0)
            {
                return;
            }

            if (!before.Contains("\r"))
            {
                Assert.DoesNotContain("\r", after);
            }

            if (!before.Contains("\n"))
            {
                Assert.DoesNotContain("\n", after);
            }

            // A CRLF document holds both characters, so neither check above says anything about
            // it - and a lone "\n" written into a CRLF file is exactly the foreign line ending
            // this is here to catch. Every CR must still be half of a pair, and every LF the
            // other half.
            if (Count(before, "\r\n") == Count(before, "\r") && Count(before, "\r\n") == Count(before, "\n"))
            {
                Assert.Equal(Count(after, "\r"), Count(after, "\r\n"));
                Assert.Equal(Count(after, "\n"), Count(after, "\r\n"));
            }
        }

        private static int Count(string text, string value)
        {
            var count = 0;

            for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal))
            {
                count++;
            }

            return count;
        }

        private static void AssertRepeatable<T>(IEnumerable<T> sequence)
            where T : SyntaxNode
        {
            var first = sequence.Select(x => x.Span.Start).ToList();
            var second = sequence.Select(x => x.Span.Start).ToList();

            Assert.Equal(first, second);
        }

        private static void AssertInBounds(TextSpan span, string text)
        {
            Assert.InRange(span.Start, 0, text.Length);
            Assert.InRange(span.Length, 0, text.Length - span.Start);
        }
    }
}
