using System;
using System.Linq;
using Microsoft.Language.Xml.Collections;
using Xunit;
using static Microsoft.Language.Xml.SyntaxFactory;

namespace Microsoft.Language.Xml.Tests
{
    public class TestLookupApis
    {
        private const string Dbml = "<l:Database xmlns:l=\"u\"><l:Table Name=\"T\" /><l:Table Name=\"U\" /></l:Database>";

        private const string Project = """
                                       <Project Sdk="Microsoft.NET.Sdk">
                                         <PropertyGroup>
                                           <TargetFramework>net8.0</TargetFramework>
                                         </PropertyGroup>
                                         <Choose>
                                           <When>
                                             <ItemGroup>
                                               <PackageReference Include="Nested" />
                                             </ItemGroup>
                                           </When>
                                         </Choose>
                                         <ItemGroup>
                                           <PackageReference Include="Top" />
                                         </ItemGroup>
                                       </Project>
                                       """;

        [Fact]
        public void GetElementByLocalNameIgnoresThePrefix()
        {
            XmlElementBaseSyntax root = Parser.ParseText(Dbml).Root!;

            Assert.Null(root.GetElement("Table"));
            Assert.Equal("T", root.GetElementByLocalName("Table")?.GetAttributeValue("Name"));
            Assert.Equal(new[] { "T", "U" }, root.GetElementsByLocalName("Table").Select(x => x.GetAttributeValue("Name")));
        }

        [Fact]
        public void GetAttributeByLocalNameIgnoresThePrefix()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<r x:id=\"1\" />").Root!;

            Assert.Null(root.GetAttribute("id"));
            Assert.Equal("1", root.GetAttributeValueByLocalName("id"));
        }

        [Fact]
        public void LookupsTakeAStringComparison()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<packages><Package Id=\"A\" /></packages>").Root!;

            Assert.Null(root.GetElement("package"));

            XmlElementBaseSyntax package = root.GetElement("package", comparison: StringComparison.OrdinalIgnoreCase);

            Assert.NotNull(package);
            Assert.Equal("A", package!.GetAttributeValue("id", comparison: StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void AnEmptyPrefixMeansUnprefixed()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<r Type=\"x\"><a /></r>").Root!;

            // GetAttribute(name, prefix) reads like GetAttribute(name, defaultValue), and passing
            // string.Empty by that mistake used to match nothing at all.
            Assert.Equal("x", root.GetAttributeValue("Type", string.Empty));
            Assert.NotNull(root.GetElement("a", string.Empty));
        }

        [Fact]
        public void DescendantsFindsElementsAtEveryDepth()
        {
            XmlElementBaseSyntax root = Parser.ParseText(Project).Root!;

            Assert.Equal(
                new[] { "Nested", "Top" },
                root.Descendants("PackageReference").Select(x => x.GetAttributeValue("Include")));
        }

        [Fact]
        public void DescendantsWithoutAFilterWalksInDocumentOrder()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<r><a><b /></a><c /></r>").Root!;

            Assert.Equal(new[] { "a", "b", "c" }, root.Descendants().Select(x => x.Name));
        }

        [Fact]
        public void DescendantsByLocalNameIgnoresThePrefix()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<r><x><l:Table /></x></r>").Root!;

            Assert.Single(root.DescendantsByLocalName("Table"));
        }

        [Fact]
        public void DescendantsCanBeEnumeratedTwice()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<r><a /><b /></r>").Root!;
            XmlDescendantElementEnumerator descendants = root.Descendants();

            Assert.Equal(2, descendants.Count());
            Assert.Equal(2, descendants.Count());
        }

        [Fact]
        public void AttributesEnumeratesTheNodes()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<r a=\"1\" b=\"2\" />").Root!;

            Assert.Equal(new[] { "a", "b" }, root.Attributes.Select(x => x.Name));
            Assert.Equal("1", root.Attributes.First().Value);
            Assert.Equal(2, root.Attributes.Count);
        }

        [Fact]
        public void AttributesHandsOutAWholeSequenceEvenAfterBeingAdvanced()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<r a=\"1\" b=\"2\" />").Root!;
            XmlAttributeNodeEnumerator attributes = root.Attributes;

            attributes.MoveNext();

            Assert.Equal(new[] { "a", "b" }, attributes.Select(x => x.Name));
        }

        [Fact]
        public void DescendantsFilterOnAPrefixAloneIsNotIgnored()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<r><l:a /><b /></r>").Root!;

            var enumerator = new XmlDescendantElementEnumerator(
                root.Content, localName: null, prefix: "l", matchAnyPrefix: false, StringComparison.Ordinal);

            Assert.Equal(new[] { "l:a" }, enumerator.Select(x => x.Name));
        }

        [Fact]
        public void DocumentLookupsTreatTheRootAsTheFirstSegment()
        {
            XmlDocumentSyntax document = Parser.ParseText(Project);

            Assert.Equal("Project", document.GetElement("Project")?.Name);
            Assert.Null(document.GetElement("PropertyGroup"));
            Assert.Equal(
                "net8.0",
                document.GetElementsByPath("Project/PropertyGroup/TargetFramework").First().Value);
        }

        [Fact]
        public void DocumentDescendantsIncludeTheRoot()
        {
            XmlDocumentSyntax document = Parser.ParseText("<r><a /></r>");

            Assert.Equal(new[] { "r", "a" }, document.Descendants().Select(x => x.Name));
        }

        [Fact]
        public void XmlEmptyElementFactoryTakesANameAndAttributes()
        {
            XmlEmptyElementSyntax element = XmlEmptyElement("PackageReference", XmlAttribute("Include", "A"));

            Assert.Equal("<PackageReference Include=\"A\" />", element.ToFullString());
        }

        [Fact]
        public void XmlEmptyElementFactoryRejectsContent()
        {
            Assert.Throws<ArgumentException>(() => XmlEmptyElement("a", "text"));
        }

        [Fact]
        public void GetOrAddElementCanSayWhichMatchItMeans()
        {
            XmlElementBaseSyntax root = Parser
                .ParseText("<Project><PropertyGroup Condition=\"x\" /><PropertyGroup /></Project>").Root!;

            root = root.GetOrAddElement(
                "PropertyGroup",
                group => group.GetAttribute("Condition") is null,
                out XmlElementBaseSyntax found);

            // Without the predicate the conditioned group wins, which is almost never what an
            // MSBuild consumer means.
            Assert.Null(found.GetAttribute("Condition"));

            // And it found that one rather than adding a third: a create-always implementation
            // would satisfy the line above just as well.
            Assert.Equal(2, root.GetElements("PropertyGroup").Count());
            Assert.Same(root.GetElements("PropertyGroup").Last(), found);
        }

        [Fact]
        public void GetOrAddElementAppliesThePredicateToTheFirstSegmentOnly()
        {
            XmlElementBaseSyntax root = Parser
                .ParseText("<Project><PropertyGroup><B Condition=\"y\">1</B></PropertyGroup></Project>").Root!;

            // A predicate written about the PropertyGroup must not also be asked about <B>, or the
            // leaf is rejected and a duplicate is created on every call.
            root = root.GetOrAddElement(
                "PropertyGroup/B",
                group => group.GetAttribute("Condition") is null,
                out XmlElementBaseSyntax b);

            Assert.Equal("1", b.Value);
            Assert.Single(root.GetElement("PropertyGroup")!.GetElements("B"));
        }

        [Fact]
        public void GetOrAddElementIsIdempotentWithAPredicate()
        {
            XmlElementBaseSyntax root = Parser
                .ParseText("<Project><PropertyGroup Condition=\"x\" /></Project>").Root!;

            Func<XmlElementBaseSyntax, bool> unconditioned = group => group.GetAttribute("Condition") is null;

            root = root.GetOrAddElement("PropertyGroup", unconditioned, out _);
            root = root.GetOrAddElement("PropertyGroup", unconditioned, out _);

            Assert.Equal(2, root.GetElements("PropertyGroup").Count());
        }

        [Fact]
        public void GetOrAddElementCreatesWhenNothingMatchesThePredicate()
        {
            XmlElementBaseSyntax root = Parser.ParseText("<Project><PropertyGroup Condition=\"x\" /></Project>").Root!;

            root = root.GetOrAddElement(
                "PropertyGroup",
                group => group.GetAttribute("Condition") is null,
                out XmlElementBaseSyntax found);

            Assert.Null(found.GetAttribute("Condition"));
            Assert.Equal(2, root.GetElements("PropertyGroup").Count());
        }
    }
}
