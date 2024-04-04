using Xunit;

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
    }
}