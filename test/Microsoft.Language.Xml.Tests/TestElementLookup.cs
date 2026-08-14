using System.Linq;
using Xunit;

namespace Microsoft.Language.Xml.Tests
{
    public class TestElementLookup
    {
        private const string WebConfig =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
                <location path="one">
                    <system.webServer>
                        <security>
                            <ipSecurity allowUnlisted="true" />
                        </security>
                    </system.webServer>
                </location>
                <location path="two">
                    <system.webServer>
                        <security>
                            <ipSecurity allowUnlisted="false" />
                        </security>
                    </system.webServer>
                </location>
            </configuration>
            """;

        [Fact]
        public void GetElementReturnsFirstMatchingChild()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root><a /><b id=\"1\" /><b id=\"2\" /></root>").Root;

            XmlElementBaseSyntax b = root.GetElement("b");

            Assert.Equal("1", b.GetAttributeValue("id"));
        }

        [Fact]
        public void GetElementReturnsNullWhenAbsent()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root><a /></root>").Root;

            Assert.Null(root.GetElement("b"));
        }

        [Fact]
        public void GetElementsReturnsEveryMatchingChild()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root><b id=\"1\" /><a /><b id=\"2\" /></root>").Root;

            var ids = root.GetElements("b").Select(x => x.GetAttributeValue("id")).ToArray();

            Assert.Equal(new[] { "1", "2" }, ids);
        }

        [Fact]
        public void GetElementsDoesNotMatchGrandchildren()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root><a><b /></a></root>").Root;

            Assert.Empty(root.GetElements("b"));
        }

        [Fact]
        public void GetElementDistinguishesPrefix()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root><x:a id=\"prefixed\" /><a id=\"plain\" /></root>").Root;

            Assert.Equal("plain", root.GetElement("a").GetAttributeValue("id"));
            Assert.Equal("prefixed", root.GetElement("a", "x").GetAttributeValue("id"));
        }

        [Fact]
        public void GetElementsByPathWalksEveryBranch()
        {
            XmlElementBaseSyntax root = Parser.ParseText(WebConfig).Root;

            var found = root
                .GetElementsByPath("location/system.webServer/security/ipSecurity")
                .Select(x => x.GetAttributeValue("allowUnlisted"))
                .ToArray();

            Assert.Equal(new[] { "true", "false" }, found);
        }

        [Fact]
        public void GetElementsByPathDistinguishesPrefix()
        {
            XmlElementBaseSyntax root = Parser.ParseText(
                "<root><a><x:b id=\"prefixed\" /><b id=\"plain\" /></a></root>").Root;

            Assert.Equal("plain", root.GetElementsByPath("a/b").First().GetAttributeValue("id"));
            Assert.Equal("prefixed", root.GetElementsByPath("a/x:b").First().GetAttributeValue("id"));
        }

        [Fact]
        public void GetElementsByPathReturnsEmptyWhenPathIsAbsent()
        {
            XmlElementBaseSyntax root = Parser.ParseText(WebConfig).Root;

            Assert.Empty(root.GetElementsByPath("location/system.web/compilation"));
        }

        [Fact]
        public void GetElementsByPathRejectsEmptySegment()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root />").Root;

            Assert.Throws<System.ArgumentException>(() => root.GetElementsByPath("a//b").ToArray());
        }

        [Fact]
        public void FirstOrDefaultReturnsNullOnAnEmptyEnumerator()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root />").Root;

            Assert.Null(root.Elements.FirstOrDefault());
            Assert.Null(root.GetElements("a").FirstOrDefault());
            Assert.Null(root.GetElementsByPath("a/b").FirstOrDefault());
        }

        [Fact]
        public void FirstThrowsOnAnEmptyEnumerator()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root />").Root;

            Assert.Throws<System.InvalidOperationException>(() => root.Elements.First());
            Assert.Throws<System.InvalidOperationException>(() => root.GetElements("a").First());
            Assert.Throws<System.InvalidOperationException>(() => root.GetElementsByPath("a/b").First());
        }

        [Fact]
        public void FirstOrDefaultDoesNotAdvanceTheCallersEnumerator()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root><b id=\"1\" /><b id=\"2\" /></root>").Root;

            var elements = root.GetElements("b");

            Assert.Equal("1", elements.FirstOrDefault().GetAttributeValue("id"));

            // The enumerator must still be positioned before the first element.
            Assert.True(elements.MoveNext());
            Assert.Equal("1", elements.Current.GetAttributeValue("id"));
        }

        [Fact]
        public void PathEnumeratorCanBeEnumeratedTwice()
        {
            XmlElementBaseSyntax root = Parser.ParseText(WebConfig).Root;

            var found = root.GetElementsByPath("location/system.webServer/security/ipSecurity");

            Assert.Equal(2, CountOf(found));
            Assert.Equal(2, CountOf(found));
        }

        private static int CountOf(Collections.XmlPathElementEnumerator enumerator)
        {
            var count = 0;

            foreach (XmlElementBaseSyntax _ in enumerator)
            {
                count++;
            }

            return count;
        }

        [Fact]
        public void GetIndentUnitIsInferredFromTheDocument()
        {
            XmlElementBaseSyntax root = Parser.ParseText(WebConfig).Root;

            Assert.Equal("    ", root.GetIndentUnit());
        }

        [Fact]
        public void GetIndentUnitFallsBackToFourSpaces()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<root><a /></root>").Root;

            Assert.Equal("    ", root.GetIndentUnit());
        }

        [Fact]
        public void GetIndentIsTheWhitespaceTheElementSitsBehind()
        {
            XmlElementBaseSyntax root = Parser.ParseText(WebConfig).Root;

            XmlElementBaseSyntax ipSecurity = root
                .GetElementsByPath("location/system.webServer/security/ipSecurity")
                .First();

            Assert.Equal("                ", ipSecurity.GetIndent());
        }

        [Fact]
        public void GetIndentIsEmptyForTheDocumentRoot()
        {
            XmlElementBaseSyntax root = Parser.ParseText(WebConfig).Root;

            Assert.Equal(string.Empty, root.GetIndent());
        }

        [Fact]
        public void GetNewLineReadsTheDocumentsLineEnding()
        {
            Assert.Equal("\n", Parser.ParseText("<root>\n    <a />\n</root>").Root.GetNewLine());
            Assert.Equal("\r\n", Parser.ParseText("<root>\r\n    <a />\r\n</root>").Root.GetNewLine());
        }

        [Fact]
        public void GetNewLineFallsBackForASingleLineDocument()
        {
            Assert.Equal("\r\n", Parser.ParseText("<root><a /></root>").Root.GetNewLine());
        }
    }
}
