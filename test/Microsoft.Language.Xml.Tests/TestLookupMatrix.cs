using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.Language.Xml.Tests
{
    /// <summary>
    /// The whole query surface against one document, as a table. Every lookup API answers about the
    /// same tree, so what each one does differently - prefixes, case, the child axis versus the
    /// descendant axis, the element entry point versus the document one - is readable side by side
    /// instead of scattered across a fact each.
    /// </summary>
    public class TestLookupMatrix
    {
        private const string Document =
            "<?xml version=\"1.0\"?>\n" +
            "<Project xmlns:p=\"urn:p\" Sdk=\"Test\">\n" +
            "  <PropertyGroup Condition=\"'$(X)' == 'a'\">\n" +
            "    <TargetFramework>net8.0</TargetFramework>\n" +
            "  </PropertyGroup>\n" +
            "  <PropertyGroup>\n" +
            "    <TargetFramework>net9.0</TargetFramework>\n" +
            "    <p:Custom>x</p:Custom>\n" +
            "  </PropertyGroup>\n" +
            "  <ItemGroup>\n" +
            "    <PackageReference Include=\"A\" />\n" +
            "    <PackageReference Include=\"B\" />\n" +
            "  </ItemGroup>\n" +
            "  <Choose>\n" +
            "    <When>\n" +
            "      <ItemGroup>\n" +
            "        <PackageReference Include=\"C\" />\n" +
            "      </ItemGroup>\n" +
            "    </When>\n" +
            "  </Choose>\n" +
            "</Project>";

        private static XmlDocumentSyntax Parsed => Parser.ParseText(Document);

        private static XmlElementBaseSyntax Root => Parsed.Root!;

        // ---------------------------------------------------------------- child axis

        [Theory]
        [InlineData("PropertyGroup", 2)]
        [InlineData("ItemGroup", 1)]      // the one under Choose/When is not a child
        [InlineData("Choose", 1)]
        [InlineData("TargetFramework", 0)]
        [InlineData("propertygroup", 0)]  // ordinal by default
        public void GetElementsWalksTheChildAxisOnly(string name, int expected)
        {
            Assert.Equal(expected, Root.GetElements(name).Count());
            Assert.Equal(expected > 0, Root.GetElement(name) is not null);
        }

        [Theory]
        [InlineData("propertygroup", 2)]
        [InlineData("PROPERTYGROUP", 2)]
        [InlineData("itemgroup", 1)]
        [InlineData("nothing", 0)]
        public void AComparisonMakesTheChildAxisCaseInsensitive(string name, int expected)
        {
            Assert.Equal(expected, Root.GetElements(name, comparison: StringComparison.OrdinalIgnoreCase).Count());
        }

        // ---------------------------------------------------------------- prefixes

        [Theory]
        // name, prefix, expected count among the second PropertyGroup's children
        [InlineData("Custom", "p", 1)]
        [InlineData("Custom", null, 0)]          // <p:Custom> is not unprefixed
        [InlineData("Custom", "", 0)]            // empty prefix means unprefixed, same as null
        [InlineData("TargetFramework", null, 1)]
        [InlineData("TargetFramework", "", 1)]
        [InlineData("TargetFramework", "p", 0)]
        public void APrefixIsPartOfTheName(string name, string? prefix, int expected)
        {
            XmlElementBaseSyntax group = Root.GetElements("PropertyGroup").Skip(1).First();

            Assert.Equal(expected, group.GetElements(name, prefix).Count());
        }

        [Theory]
        [InlineData("Custom", 1)]
        [InlineData("TargetFramework", 1)]
        public void ByLocalNameMatchesWhateverThePrefix(string name, int expected)
        {
            XmlElementBaseSyntax group = Root.GetElements("PropertyGroup").Skip(1).First();

            Assert.Equal(expected, group.GetElementsByLocalName(name).Count());
            Assert.NotNull(group.GetElementByLocalName(name));
        }

        // ---------------------------------------------------------------- descendant axis

        [Theory]
        [InlineData("PackageReference", 3)]   // including the one under Choose/When
        [InlineData("ItemGroup", 2)]
        [InlineData("TargetFramework", 2)]
        [InlineData("Project", 0)]            // the root is not below itself
        public void DescendantsWalksTheWholeSubtree(string name, int expected)
        {
            Assert.Equal(expected, Root.Descendants(name).Count());
        }

        [Fact]
        public void DescendantsOnTheDocumentIncludesTheRoot()
        {
            Assert.Single(Parsed.Descendants("Project"));
            Assert.Equal(Root.Descendants().Count() + 1, Parsed.Descendants().Count());
        }

        [Fact]
        public void DescendantsCanFilterByPrefixAlone()
        {
            Assert.Single(Root.Descendants(null, "p"));
            Assert.Empty(Root.Descendants(null, "zzz"));
        }

        // ---------------------------------------------------------------- paths

        [Theory]
        [InlineData("PropertyGroup", 2)]
        [InlineData("PropertyGroup/TargetFramework", 2)]   // every segment expands, not just the first
        [InlineData("ItemGroup/PackageReference", 2)]
        [InlineData("Choose/When/ItemGroup/PackageReference", 1)]
        [InlineData("PropertyGroup/p:Custom", 1)]
        [InlineData("PropertyGroup/Custom", 0)]
        [InlineData("Missing/Path", 0)]
        public void APathExpandsEverySegment(string path, int expected)
        {
            Assert.Equal(expected, Root.GetElementsByPath(path).Count());
            Assert.Equal(expected, Root.GetElementsByPath("/" + path).Count());
        }

        [Theory]
        [InlineData("Project/PropertyGroup", 2)]
        [InlineData("Project/ItemGroup/PackageReference", 2)]
        [InlineData("PropertyGroup", 0)]   // the document's first segment is the root element
        public void ADocumentPathStartsAtTheRootElement(string path, int expected)
        {
            Assert.Equal(expected, Parsed.GetElementsByPath(path).Count());
        }

        // ---------------------------------------------------------------- attributes

        [Theory]
        [InlineData("Sdk", null, "Test")]
        [InlineData("sdk", null, null)]
        [InlineData("p", "xmlns", "urn:p")]
        [InlineData("xmlns:p", null, null)]   // the prefix is not part of the local name
        [InlineData("Missing", null, null)]
        public void AnAttributeIsFoundByNameAndPrefix(string name, string? prefix, string? expected)
        {
            Assert.Equal(expected, Root.GetAttributeValue(name, prefix));
        }

        [Theory]
        [InlineData("sdk", "Test")]
        [InlineData("SDK", "Test")]
        [InlineData("missing", null)]
        public void AComparisonMakesAttributeLookupCaseInsensitive(string name, string? expected)
        {
            Assert.Equal(expected, Root.GetAttributeValue(name, comparison: StringComparison.OrdinalIgnoreCase));
        }

        [Theory]
        [InlineData("p", "urn:p")]      // xmlns:p, whatever the prefix
        [InlineData("Sdk", "Test")]
        public void ByLocalNameFindsAnAttributeWhateverThePrefix(string name, string expected)
        {
            Assert.Equal(expected, Root.GetAttributeValueByLocalName(name));
        }

        [Fact]
        public void AttributesEnumeratesTheNodesThemselves()
        {
            var names = Root.Attributes.Select(x => x.Name).ToList();

            Assert.Equal(new[] { "xmlns:p", "Sdk" }, names);
            Assert.Equal(2, Root.Attributes.Count);
        }

        // ---------------------------------------------------------------- entry points agree

        public static TheoryData<string> ChildNames => new() { "PropertyGroup", "ItemGroup", "Choose", "Missing" };

        [Theory]
        [MemberData(nameof(ChildNames))]
        public void TheDocumentAndTheRootAgreeBelowTheRoot(string name)
        {
            Assert.Equal(
                Root.Descendants(name).Select(x => x.Span.Start),
                Parsed.Descendants(name).Select(x => x.Span.Start));

            Assert.Equal(
                Root.GetElements(name).Select(x => x.Span.Start),
                Parsed.GetElementsByPath("Project/" + name).Select(x => x.Span.Start));
        }

        [Theory]
        [MemberData(nameof(ChildNames))]
        public void FirstOrDefaultAgreesWithTheFullEnumeration(string name)
        {
            // The enumerator's own FirstOrDefault, which is the one that skips LINQ - taken from
            // the front of the sequence LINQ walks, not from another call to it.
            List<XmlElementBaseSyntax> all = Root.GetElements(name).ToList();
            int? expected = all.Count > 0 ? all[0].Span.Start : null;

            Assert.Equal(expected, Root.GetElements(name).FirstOrDefault()?.Span.Start);
            Assert.Equal(expected, Root.GetElement(name)?.Span.Start);
        }
    }
}
