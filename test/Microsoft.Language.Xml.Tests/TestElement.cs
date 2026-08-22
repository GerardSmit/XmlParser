using System;
using Xunit;
using static Microsoft.Language.Xml.SyntaxFactory;
using XmlAttribute = System.Xml.XmlAttribute;

namespace Microsoft.Language.Xml.Tests
{
    public class TestElement
    {
        [Fact]
        public void SetAttributeEmpty()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root />").Root;

            root = root.SetAttribute("attr", "value");

            Assert.Equal("<root attr=\"value\" />", root.ToFullString());
        }

        [Fact]
        public void SetAttributeContent()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root></root>").Root;

            root = root.SetAttribute("attr", "value");

            Assert.Equal("<root attr=\"value\"></root>", root.ToFullString());
        }

        [Fact]
        public void SetAttributeExistingEmpty()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root foo=\"bar\" />").Root;

            root = root.SetAttribute("attr", "value");

            Assert.Equal("<root foo=\"bar\" attr=\"value\" />", root.ToFullString());
        }

        [Fact]
        public void SetAttributeExistingContent()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root foo=\"bar\"></root>").Root;

            root = root.SetAttribute("attr", "value");

            Assert.Equal("<root foo=\"bar\" attr=\"value\"></root>", root.ToFullString());
        }

        [Fact]
        public void SetAttributeExistingContentTrivia()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root attr=\"old\" foo=\"bar\"></root>").Root;

            root = root.SetAttribute("attr", "value");

            Assert.Equal("<root attr=\"value\" foo=\"bar\"></root>", root.ToFullString());
        }

        [Fact]
        public void GetOrAddChildElement()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root></root>").Root;

            root = root.GetOrAddElement("a", out XmlElementBaseSyntax a);

            Assert.Equal("<root>\r\n  <a />\r\n</root>", root.ToFullString());
        }

        [Fact]
        public void GetOrAddChildEmpty()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root />").Root;

            root = root.GetOrAddElement("a", out XmlElementBaseSyntax a);

            Assert.Equal("<root>\r\n  <a />\r\n</root>", root.ToFullString());
        }

        [Fact]
        public void GetOrAddChildExistingSelfClosing()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root><a /></root>").Root;

            root = root.GetOrAddElement("a", out XmlElementBaseSyntax a);

            Assert.Equal("<root><a /></root>", root.ToFullString());
            Assert.Equal("a", a.Name);
        }

        [Fact]
        public void GetOrAddChildPathThroughSelfClosing()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root><a /></root>").Root;

            root = root.GetOrAddElement("a/b", out XmlElementBaseSyntax b);

            Assert.Equal("b", b.Name);
            Assert.Equal("<root><a><b /></a></root>", root.ToFullString());
        }

        [Fact]
        public void PromotingASelfClosingElementDropsTheAttributeSeparator()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<ipSecurity />").Root;

            // indent: false keeps the assertion about the start tag rather than the layout.
            root = root.SetAttribute("allowUnlisted", "false").AddChild(XmlElement("add"), indent: false);

            Assert.Equal("<ipSecurity allowUnlisted=\"false\"><add /></ipSecurity>", root.ToFullString());
        }

        [Fact]
        public void PromotingASelfClosingElementWithNoAttributesDropsTheNameSeparator()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<ipSecurity />").Root;

            Assert.Equal("<ipSecurity><add /></ipSecurity>", root.AddChild(XmlElement("add"), indent: false).ToFullString());
        }

        [Fact]
        public void PromotingASelfClosingElementKeepsTheNameSeparatorItAlreadyHad()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<ipSecurity allowUnlisted=\"false\"/>").Root;

            Assert.Equal(
                "<ipSecurity allowUnlisted=\"false\"><add /></ipSecurity>",
                root.AddChild(XmlElement("add"), indent: false).ToFullString());
        }

        [Fact]
        public void PromotingASelfClosingElementDropsOnlyTheLastAttributesSeparator()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<ipSecurity a=\"1\"\r\n            b=\"2\" />").Root;

            // Whatever sat directly in front of the "/>" belongs to that token and goes with it;
            // the line break separating the attributes is the last attribute's own leading trivia
            // and stays put.
            Assert.Equal(
                "<ipSecurity a=\"1\"\r\n            b=\"2\"><add /></ipSecurity>",
                root.AddChild(XmlElement("add"), indent: false).ToFullString());
        }

        [Fact]
        public void AddChildPutsTheChildOnItsOwnLine()
        {
            const string Text = "<Project>\r\n\t<ItemGroup>\r\n\t\t<PackageReference Include=\"A\" />\r\n\t</ItemGroup>\r\n</Project>";
            XmlElementBaseSyntax root = Parser.ParseText(Text).Root;
            XmlElementBaseSyntax group = root.GetElement("ItemGroup")!;

            XmlElementSyntax newGroup = group.AddChild(XmlEmptyElement("PackageReference", XmlAttribute("Include", "B")));

            Assert.Equal(
                "\r\n\t<ItemGroup>\r\n\t\t<PackageReference Include=\"A\" />\r\n\t\t<PackageReference Include=\"B\" />\r\n\t</ItemGroup>",
                newGroup.ToFullString());
        }

        [Fact]
        public void AddChildOpensASelfClosingParentOntoItsOwnLines()
        {
            const string Text = "<Project>\r\n\t<ItemGroup />\r\n</Project>";
            XmlElementBaseSyntax root = Parser.ParseText(Text).Root;
            XmlElementBaseSyntax group = root.GetElement("ItemGroup")!;

            XmlElementSyntax newGroup = group.AddChild(XmlEmptyElement("PackageReference", XmlAttribute("Include", "B")));

            Assert.Equal(
                "\r\n\t<ItemGroup>\r\n\t\t<PackageReference Include=\"B\" />\r\n\t</ItemGroup>",
                newGroup.ToFullString());
        }

        [Fact]
        public void ChainedAddChildKeepsEveryChildAtTheSameLevel()
        {
            const string Text = "<P>\r\n  <G>\r\n    <A />\r\n  </G>\r\n</P>";
            XmlElementBaseSyntax group = Parser.ParseText(Text).Root.GetElement("G")!;

            // The element the first AddChild hands back is detached, so anything that works out the
            // indent from the node's depth in the document under-counts it and the second child
            // lands a level too deep.
            XmlElementSyntax result = group
                .AddChild(XmlEmptyElement("B"))
                .AddChild(XmlEmptyElement("C"));

            Assert.Equal(
                "\r\n  <G>\r\n    <A />\r\n    <B />\r\n    <C />\r\n  </G>",
                result.ToFullString());
        }

        [Fact]
        public void ChainedAddChildKeepsItsLevelWhenTheFirstChildIsWeldedToTheStartTag()
        {
            const string Text = "<P>\r\n  <G><A />\r\n  </G>\r\n</P>";
            XmlElementBaseSyntax group = Parser.ParseText(Text).Root.GetElement("G")!;

            // <A /> starts on the same line as <G>, so it has no indentation to copy. The child the
            // first AddChild leaves behind does, which is why the last indented child is the one to
            // look at rather than the first.
            XmlElementSyntax result = group
                .AddChild(XmlEmptyElement("B"))
                .AddChild(XmlEmptyElement("C"));

            Assert.Equal(
                "\r\n  <G><A />\r\n    <B />\r\n    <C />\r\n  </G>",
                result.ToFullString());
        }

        [Fact]
        public void AddChildUsesTheLineEndingTheDocumentAlreadyUses()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<doc>\n  <a />\n</doc>").Root;

            Assert.Equal("<doc>\n  <a />\n  <b />\n</doc>", root.AddChild(XmlEmptyElement("b")).ToFullString());
        }

        [Fact]
        public void AddChildLeavesASingleLineElementOnOneLine()
        {
            // <B /> is separated from <A /> by a space, not a line break, so the element is laid out
            // inline and the end tag has no business moving to a line of its own.
            XmlElementBaseSyntax group = Parser.ParseText("<P><G><A /> <B /></G></P>").Root.GetElement("G")!;

            Assert.Equal("<G><A /> <B /> <C /></G>", group.AddChild(XmlEmptyElement("C")).ToFullString());
        }

        [Fact]
        public void NormalizeTriviaIndentsGrandchildrenPastTheirParent()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<Project>\r\n  <A />\r\n</Project>").Root;

            XmlElementBaseSyntax added = XmlElement("G", XmlEmptyElement("H")).NormalizeTrivia(root);

            Assert.Equal("\r\n  <G>\r\n    <H />\r\n  </G>", added.ToFullString());
        }

        [Fact]
        public void NormalizeTriviaLeavesNoBlankLineInAnUnindentedDocument()
        {
            // Every child sits at column zero, so the sibling's leading trivia is the line break
            // itself. Treating that as the indent would repeat it and leave a blank line.
            XmlElementBaseSyntax root = Parser.ParseText("<G>\n<A />\n</G>").Root;

            XmlElementBaseSyntax added = XmlElement("P", XmlEmptyElement("Q")).NormalizeTrivia(root);

            Assert.Equal("\n<P>\n  <Q />\n</P>", added.ToFullString());
        }

        [Fact]
        public void AddChildIndentsPastAnAncestorIndentedLessThanOneUnitPerLevel()
        {
            // <d> is three levels deep behind two spaces, so scaling that indent by the depth
            // divides to nothing. The document's own unit has to answer instead, or the child
            // lands at exactly its parent's column.
            const string Text = "<a>\r\n<b>\r\n<c>\r\n  <d></d>\r\n</c>\r\n</b>\r\n</a>";
            XmlElementBaseSyntax d = Parser.ParseText(Text).Root.GetElementsByPath("b/c/d").First();

            Assert.Equal("\r\n  <d>\r\n    <e />\r\n  </d>", d.AddChild(XmlEmptyElement("e")).ToFullString());
        }

        [Fact]
        public void AddChildFindsTheLineEndingWhereverTheDocumentPutIt()
        {
            // The only line break is in front of the end tag - not in front of any element - so a
            // scan over elements alone answers CRLF and mixes the two endings in one file.
            XmlElementBaseSyntax root = Parser.ParseText("<G><A />\n</G>").Root;

            Assert.Equal("\n", root.GetNewLine());
            Assert.DoesNotContain("\r", root.AddChild(XmlEmptyElement("C")).ToFullString());
        }

        [Theory]
        // A line break inside text, a comment or a CDATA section is part of the token, not trivia,
        // and it is still the line ending the file is written with.
        [InlineData("<a>text\nmore</a>")]
        [InlineData("<a><!--\ncomment--><b /></a>")]
        [InlineData("<a><![CDATA[\n]]></a>")]
        public void GetNewLineSeesLineBreaksInsideTokens(string text)
        {
            Assert.Equal("\n", Parser.ParseText(text).Root.GetNewLine());
        }

        [Fact]
        public void GetNewLineSeesAFinalNewlinePastTheRootElement()
        {
            // A minified file with a POSIX final newline keeps its only line break on the
            // document's last token, outside the root element's subtree.
            Assert.Equal("\n", Parser.ParseText("<r><a /></r>\n").Root.GetNewLine());
        }

        [Fact]
        public void AddChildIndentsWithTheDocumentsUnitWhenAnAncestorIsUnderIndented()
        {
            // <d> is at two tabs but three levels deep, so its own indent divides to nothing and
            // the document's unit answers instead - the document's, not the element's, or a tab
            // file grows spaces.
            const string Text = "<a>\n\t<b>\n\t\t<c>\n\t\t<d></d>\n\t\t</c>\n\t</b>\n</a>";
            XmlElementBaseSyntax d = Parser.ParseText(Text).Root.GetElementsByPath("b/c/d").First();

            Assert.Equal("\n\t\t<d>\n\t\t\t<e />\n\t\t</d>", d.AddChild(XmlEmptyElement("e")).ToFullString());
        }

        [Fact]
        public void AddChildWithoutIndentingWeldsTheChildToItsSibling()
        {
            const string Text = "<Project>\r\n\t<ItemGroup>\r\n\t\t<PackageReference Include=\"A\" />\r\n\t</ItemGroup>\r\n</Project>";
            XmlElementBaseSyntax root = Parser.ParseText(Text).Root;
            XmlElementBaseSyntax group = root.GetElement("ItemGroup")!;

            XmlElementSyntax newGroup = group.AddChild(XmlElement("b"), indent: false);

            Assert.Equal(
                "\r\n\t<ItemGroup>\r\n\t\t<PackageReference Include=\"A\" /><b />\r\n\t</ItemGroup>",
                newGroup.ToFullString());
        }

        [Fact]
        public void GetOrAddElementThroughASelfClosingElementDropsTheAttributeSeparator()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<PropertyGroup Condition=\"x\" />").Root;

            root = root.GetOrAddElement("B", out _);

            Assert.Equal("<PropertyGroup Condition=\"x\">\r\n  <B />\r\n</PropertyGroup>", root.ToFullString());
        }

        [Theory]
        [InlineData("")]
        [InlineData("/")]
        [InlineData("a//b")]
        [InlineData("a/b/")]
        public void GetOrAddElementRejectsAnEmptySegment(string path)
        {
            XmlElementBaseSyntax root = Parser.ParseText("<r />").Root;

            Assert.Throws<ArgumentException>(() => root.GetOrAddElement(path, out _));
        }

        [Theory]
        [InlineData("")]
        [InlineData("/")]
        [InlineData("a//b")]
        [InlineData("a/b/")]
        public void AddElementRejectsAnEmptySegment(string path)
        {
            XmlElementBaseSyntax root = Parser.ParseText("<r />").Root;

            Assert.Throws<ArgumentException>(() => root.AddElement(path, out _));
        }

        [Fact]
        public void GetOrAddElementAcceptsALeadingSlash()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root><a /></root>").Root;

            root = root.GetOrAddElement("/a/b", out XmlElementBaseSyntax b);

            Assert.Equal("b", b.Name);
            Assert.Equal("<root><a><b /></a></root>", root.ToFullString());
        }

        [Fact]
        public void GetOrAddElementAcceptsALeadingSlashOnASingleSegment()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root><a /></root>").Root;

            root = root.GetOrAddElement("/a", out XmlElementBaseSyntax a);

            Assert.Equal("a", a.Name);
            Assert.Equal("<root><a /></root>", root.ToFullString());
        }

        [Fact]
        public void GetOrAddChildElementMultiple()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root></root>").Root;

            root = root.GetOrAddElement("a", out XmlElementBaseSyntax a);

            XmlElementSyntax newA = a.GetOrAddElement("b", out XmlElementBaseSyntax b);
            root = root.ReplaceNode(a, newA);

            Assert.Equal("<root>\r\n  <a>\r\n    <b />\r\n  </a>\r\n</root>", root.ToFullString());
        }

        [Fact]
        public void GetOrAddChildElement2()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root></root>").Root;

            root = root.GetOrAddElement("a/b", out XmlElementBaseSyntax a);

            Assert.Equal("<root>\r\n  <a>\r\n    <b />\r\n  </a>\r\n</root>", root.ToFullString());
        }

        [Fact]
        public void GetOrAddChildElement3()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root></root>").Root;

            root = root.GetOrAddElement("a/b/c", out XmlElementBaseSyntax c);

            Assert.Equal("<root>\r\n  <a>\r\n    <b>\r\n      <c />\r\n    </b>\r\n  </a>\r\n</root>", root.ToFullString());
        }

        [Fact]
        public void GetOrAddChildElement4()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root></root>").Root;

            root = root.GetOrAddElement("a/b/c/d", out XmlElementBaseSyntax d);

            Assert.Equal("<root>\r\n  <a>\r\n    <b>\r\n      <c>\r\n        <d />\r\n      </c>\r\n    </b>\r\n  </a>\r\n</root>", root.ToFullString());
        }

        [Fact]
        public void GetOrAddChildElement4Text()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root><a>Test</a></root>").Root;

            root = root.GetOrAddElement("a/b/c/d", out XmlElementBaseSyntax d);

            Assert.Equal("<root><a>Test<b><c><d /></c></b></a></root>", root.ToFullString());
        }

        [Fact]
        public void GetOrAddChildElement4Multiple()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root><foo /><a><foo /></a></root>").Root;

            root = root.GetOrAddElement("a/b/c/d", out XmlElementBaseSyntax d);

            Assert.Equal("<root><foo /><a><foo /><b><c><d /></c></b></a></root>", root.ToFullString());
        }

        [Fact]
        public void GetOrAddChildElementExisting4()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root><a><b><c></c></b></a></root>").Root;

            root = root.GetOrAddElement("a/b/c/d", out XmlElementBaseSyntax d);

            Assert.Equal("<root><a><b><c><d /></c></b></a></root>", root.ToFullString());
        }

        [Fact]
        public void GetOrAddChildElementWhitespace4()
        {
            XmlElementBaseSyntax root = Parser.ParseText(
                """
                <root>
                    <a></a>
                </root>
                """).Root;

            root = root.GetOrAddElement("a/b/c/d", out XmlElementBaseSyntax d);

            Assert.Equal("""
                         <root>
                             <a>
                                 <b>
                                     <c>
                                         <d />
                                     </c>
                                 </b>
                             </a>
                         </root>
                         """, root.ToFullString());
        }

        [Fact]
        public void GetOrAddChildElementExistingWhitespace4()
        {
            XmlElementBaseSyntax root = Parser.ParseText(
                """
                <root>
                    <a>
                        <b>
                            <c>
                            </c>
                        </b>
                    </a>
                </root>
                """).Root;

            root = root.GetOrAddElement("a/b/c/d", out XmlElementBaseSyntax d);

            Assert.Equal("""
                         <root>
                             <a>
                                 <b>
                                     <c>
                                         <d />
                                     </c>
                                 </b>
                             </a>
                         </root>
                         """, root.ToFullString());
        }

        [Fact]
        public void ReplaceDocumentBody()
        {
            XmlDocumentSyntax root = Parser.ParseText(
                """
                <?xml version="1.0" encoding="utf-8"?>
                <xml />
                """);

            root = root.ReplaceNode(
                root.Root,
                root.Root.SetAttribute("attr", "value")
            );

            Assert.Equal("""
                         <?xml version="1.0" encoding="utf-8"?>
                         <xml attr="value" />
                         """, root.ToFullString());
        }

        [Fact]
        public void AddChildElementExistingWhitespace4()
        {
            XmlElementBaseSyntax root = Parser.ParseText(
                """
                <root>
                    <a>
                        <b>
                            <c>
                                <d />
                            </c>
                        </b>
                    </a>
                </root>
                """).Root;

            root = root.AddElement("a/b/c/d", out XmlElementBaseSyntax d);

            Assert.Equal(
                """
                <root>
                    <a>
                        <b>
                            <c>
                                <d />
                                <d />
                            </c>
                        </b>
                    </a>
                </root>
                """, root.ToFullString());
        }

        [Fact]
        public void XDocumentLike()
        {
            XmlElementBaseSyntax root = Parser.ParseText(
                """
                <configuration>
                <system.web>
                     <test />
                 </system.web>
                    <system.webServer>
                        <rewrite>
                            <rules>
                            </rules>
                        </rewrite>
                    </system.webServer>
                </configuration>
                """
                ).Root;

            root = root.GetOrAddElement("system.webServer/rewrite/rules", out XmlElementBaseSyntax rules);

            root = root.ReplaceNode(
                rules,
                rules.AddChild(XmlElement(
                    "rule",
                    XmlAttribute("name", "rule1"),
                    XmlAttribute("enabled", "true"),
                    XmlElement("match",
                        XmlAttribute("url", "pattern"),
                        XmlAttribute("negate", "false"),
                        XmlAttribute("test", null)
                    ),
                    null,
                    XmlElement("action",
                        XmlAttribute("type", "Rewrite"),
                        XmlAttribute("url", "http://example.com")
                    )
                ).NormalizeTrivia(rules))
            );

            Assert.Equal(
                """
                <configuration>
                <system.web>
                     <test />
                 </system.web>
                    <system.webServer>
                        <rewrite>
                            <rules>
                                <rule name="rule1" enabled="true">
                                    <match url="pattern" negate="false" test />
                                    <action type="Rewrite" url="http://example.com" />
                                </rule>
                            </rules>
                        </rewrite>
                    </system.webServer>
                </configuration>
                """, root.ToFullString());
        }

        [Fact]
        public void NormalizeTriviaUsesTheInsertionParentsIndentAtEveryLevel()
        {
            XmlElementBaseSyntax root = Parser.ParseText(
                "<configuration>\r\n\t<system.webServer>\r\n\t\t<rewrite />\r\n\t</system.webServer>\r\n</configuration>").Root;

            XmlElementBaseSyntax parent = root.GetElement("system.webServer");
            XmlElementBaseSyntax newParent = parent.AddChild(
                XmlElement("security", XmlElement("ipSecurity")).NormalizeTrivia(parent));

            Assert.Equal(
                "<configuration>\r\n\t<system.webServer>\r\n\t\t<rewrite />\r\n\t\t<security>\r\n\t\t\t<ipSecurity />\r\n\t\t</security>\r\n\t</system.webServer>\r\n</configuration>",
                root.ReplaceNode(parent, newParent).ToFullString());
        }

        [Fact]
        public void NormalizeTriviaIndentsChildAddedToRootElement()
        {
            XmlDocumentSyntax doc = Parser.ParseText(
                """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                    <system.web>
                        <compilation debug="true" />
                    </system.web>
                </configuration>
                """);

            XmlElementBaseSyntax root = doc.Root;
            XmlElementBaseSyntax newRoot = root.AddChild(XmlElement("appSettings").NormalizeTrivia(root));

            Assert.Equal(
                """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                    <system.web>
                        <compilation debug="true" />
                    </system.web>
                    <appSettings />
                </configuration>
                """, doc.ReplaceNode(root, newRoot).ToFullString());
        }

        [Fact]
        public void NormalizeTriviaIndentsNestedChildAddedToRootElement()
        {
            XmlDocumentSyntax doc = Parser.ParseText(
                """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                    <system.web>
                        <compilation debug="true" />
                    </system.web>
                </configuration>
                """);

            XmlElementBaseSyntax root = doc.Root;
            XmlElementBaseSyntax newRoot = root.AddChild(
                XmlElement("system.webServer", XmlElement("security", XmlElement("ipSecurity")))
                    .NormalizeTrivia(root));

            Assert.Equal(
                """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                    <system.web>
                        <compilation debug="true" />
                    </system.web>
                    <system.webServer>
                        <security>
                            <ipSecurity />
                        </security>
                    </system.webServer>
                </configuration>
                """, doc.ReplaceNode(root, newRoot).ToFullString());
        }

        [Fact]
        public void NormalizeTriviaAlignsTheEndTagOfASubtreeBuiltWithAddElement()
        {
            // AddElement on a detached subtree stamps a bare line break in front of the end tag -
            // a placeholder, correct only at column zero. Normalizing against the insertion parent
            // has to replace it, or the closing tag stays at column zero however deep the rest of
            // the subtree is indented.
            XmlDocumentSyntax tree = Parser.ParseText(
                "<root>\r\n\t<outer>\r\n\t\t<existing />\r\n\t</outer>\r\n</root>\r\n");
            XmlElementBaseSyntax root = tree.Root;
            XmlElementBaseSyntax outer = root.GetElement("outer");

            XmlElementSyntax built = (XmlElementSyntax)Parser.ParseText("<added></added>").Root;
            built = built.AddElement("child", out _, (_, e) => e.SetAttribute("a", "1"));

            XmlElementBaseSyntax newRoot = root.ReplaceNode(
                outer,
                outer.AddChild(built.NormalizeTrivia(outer), indent: false));

            Assert.Equal(
                "<root>\r\n\t<outer>\r\n\t\t<existing />\r\n\t\t<added>\r\n\t\t\t<child a=\"1\" />\r\n\t\t</added>\r\n\t</outer>\r\n</root>\r\n",
                tree.ReplaceNode(tree.Root, newRoot).ToFullString());
        }

        [Fact]
        public void NormalizeTriviaAlignsEndTagsAtEveryLevelOfAPathBuiltSubtree()
        {
            // An intermediate element created by a path gets a guessed indent in front of its end
            // tag, not just a bare line break - the subtree was still detached, so the guess used
            // the fallback unit. Normalizing rewrites every other line's layout, so it rewrites
            // this one too.
            XmlDocumentSyntax tree = Parser.ParseText(
                "<root>\r\n\t<outer>\r\n\t\t<existing />\r\n\t</outer>\r\n</root>\r\n");
            XmlElementBaseSyntax root = tree.Root;
            XmlElementBaseSyntax outer = root.GetElement("outer");

            XmlElementSyntax built = (XmlElementSyntax)Parser.ParseText("<added></added>").Root;
            built = built.AddElement("child/grand", out _);

            XmlElementBaseSyntax newRoot = root.ReplaceNode(
                outer,
                outer.AddChild(built.NormalizeTrivia(outer), indent: false));

            Assert.Equal(
                "<root>\r\n\t<outer>\r\n\t\t<existing />\r\n\t\t<added>\r\n\t\t\t<child>\r\n\t\t\t\t<grand />\r\n\t\t\t</child>\r\n\t\t</added>\r\n\t</outer>\r\n</root>\r\n",
                tree.ReplaceNode(tree.Root, newRoot).ToFullString());
        }

        [Fact]
        public void NormalizeTriviaTakesTheDocumentsLineEndingForTheEndTag()
        {
            // The placeholder line break was guessed while the subtree was detached, so it is CRLF
            // whatever the document does. The start tag's line break comes from the document, and
            // realigning copies it rather than keeping the guess.
            XmlDocumentSyntax tree = Parser.ParseText("<root>\n  <outer>\n    <existing />\n  </outer>\n</root>\n");
            XmlElementBaseSyntax outer = tree.Root.GetElement("outer");

            XmlElementSyntax built = (XmlElementSyntax)Parser.ParseText("<added></added>").Root;
            built = built.AddElement("child", out _);

            Assert.Equal(
                "\n    <added>\n      <child />\n    </added>",
                built.NormalizeTrivia(outer).ToFullString());
        }

        [Fact]
        public void NormalizeTriviaLeavesATextOnlyChildsEndTagAlone()
        {
            // Whitespace in front of an end tag is part of what the element says. An element
            // holding nothing but text is not laid out as a block, so normalizing must not put its
            // end tag on a line of its own - that would change the value, not the layout.
            XmlDocumentSyntax tree = Parser.ParseText("<root>\n  <outer>\n    <existing />\n  </outer>\n</root>\n");
            XmlElementBaseSyntax outer = tree.Root.GetElement("outer");

            XmlElementSyntax built = (XmlElementSyntax)Parser.ParseText("<added><name>hello</name></added>").Root;
            XmlElementSyntax normalized = built.NormalizeTrivia(outer);

            Assert.Equal("hello", normalized.GetElement("name").Value);
            Assert.Equal(
                "\n    <added>\n      <name>hello</name>\n    </added>",
                normalized.ToFullString());
        }

        [Fact]
        public void NormalizeTriviaLeavesAnEmptyElementsTagsTogether()
        {
            // <empty></empty> holds nothing, so there is no block layout to give it - splitting
            // its tags would turn an empty value into a whitespace one.
            XmlDocumentSyntax tree = Parser.ParseText("<root>\n  <outer>\n    <existing />\n  </outer>\n</root>\n");
            XmlElementBaseSyntax outer = tree.Root.GetElement("outer");

            XmlElementSyntax built = (XmlElementSyntax)Parser.ParseText("<added><empty></empty></added>").Root;

            Assert.Equal(
                "\n    <added>\n      <empty></empty>\n    </added>",
                built.NormalizeTrivia(outer).ToFullString());
        }
    }
}
