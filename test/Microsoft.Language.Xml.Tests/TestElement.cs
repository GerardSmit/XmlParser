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

            root = root.GetOrAddElement("a", out XmlElementSyntax a);

            Assert.Equal("<root><a></a></root>", root.ToFullString());
        }

        [Fact]
        public void GetOrAddChildEmpty()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root />").Root;

            root = root.GetOrAddElement("a", out XmlElementSyntax a);

            Assert.Equal("<root><a></a></root>", root.ToFullString());
        }

        [Fact]
        public void GetOrAddChildElementMultiple()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root></root>").Root;

            root = root.GetOrAddElement("a", out XmlElementSyntax a);

            XmlElementSyntax newA = a.GetOrAddElement("b", out XmlElementSyntax b);
            root = root.ReplaceNode(a, newA);

            Assert.Equal("<root><a><b></b></a></root>", root.ToFullString());
        }

        [Fact]
        public void GetOrAddChildElement2()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root></root>").Root;

            root = root.GetOrAddElement("a/b", out XmlElementSyntax a);

            Assert.Equal("<root><a><b></b></a></root>", root.ToFullString());
        }

        [Fact]
        public void GetOrAddChildElement3()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root></root>").Root;

            root = root.GetOrAddElement("a/b/c", out XmlElementSyntax c);

            Assert.Equal("<root><a><b><c></c></b></a></root>", root.ToFullString());
        }

        [Fact]
        public void GetOrAddChildElement4()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root></root>").Root;

            root = root.GetOrAddElement("a/b/c/d", out XmlElementSyntax d);

            Assert.Equal("<root><a><b><c><d></d></c></b></a></root>", root.ToFullString());
        }
    }
}
