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
        public void SetAttributeExitingEmpty()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root foo=\"bar\" />").Root;

            root = root.SetAttribute("attr", "value");

            Assert.Equal("<root foo=\"bar\" attr=\"value\" />", root.ToFullString());
        }

        [Fact]
        public void SetAttributeExitingContent()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root foo=\"bar\"></root>").Root;

            root = root.SetAttribute("attr", "value");

            Assert.Equal("<root foo=\"bar\" attr=\"value\"></root>", root.ToFullString());
        }

        [Fact]
        public void GetOrAddChildElement()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root></root>").Root;

            root = root.GetOrAddElement("a", out XmlElementBaseSyntax a);

            Assert.Equal("<root><a /></root>", root.ToFullString());
        }

        [Fact]
        public void GetOrAddChildEmpty()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root />").Root;

            root = root.GetOrAddElement("a", out XmlElementBaseSyntax a);

            Assert.Equal("<root><a /></root>", root.ToFullString());
        }

        [Fact]
        public void GetOrAddChildElementMultiple()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root></root>").Root;

            root = root.GetOrAddElement("a", out XmlElementBaseSyntax a);

            XmlElementSyntax newA = a.GetOrAddElement("b", out XmlElementBaseSyntax b);
            root = root.ReplaceNode(a, newA);

            Assert.Equal("<root><a><b /></a></root>", root.ToFullString());
        }

        [Fact]
        public void GetOrAddChildElement2()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root></root>").Root;

            root = root.GetOrAddElement("a/b", out XmlElementBaseSyntax a);

            Assert.Equal("<root><a><b /></a></root>", root.ToFullString());
        }

        [Fact]
        public void GetOrAddChildElement3()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root></root>").Root;

            root = root.GetOrAddElement("a/b/c", out XmlElementBaseSyntax c);

            Assert.Equal("<root><a><b><c /></b></a></root>", root.ToFullString());
        }

        [Fact]
        public void GetOrAddChildElement4()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root></root>").Root;

            root = root.GetOrAddElement("a/b/c/d", out XmlElementBaseSyntax d);

            Assert.Equal("<root><a><b><c><d /></c></b></a></root>", root.ToFullString());
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
    }
}
