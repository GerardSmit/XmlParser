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
        public void GetOrAddChildElementNested()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root></root>").Root;

            root = root.GetOrAddElement("a", out XmlElementSyntax a);

            XmlElementSyntax newA = a.GetOrAddElement("b", out XmlElementSyntax b);
            root = root.ReplaceNode(a, newA);

            Assert.Equal("<root><a><b></b></a></root>", root.ToFullString());
        }
    }
}
