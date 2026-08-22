# XmlParser

> [!NOTE]  
> This is a fork of the 'GuiLabs.Language.Xml' project. See [KirillOsenkov/XmlParser](https://github.com/KirillOsenkov/XmlParser) for the original project. This project is not affiliated with the original project (the namespace and the project name are the same for compatibility reasons).

## Changes
In comparison to the original project, this fork has the following changes:

> [!IMPORTANT]
> 3.0.0 changes what two existing members mean. `Value` — on elements and attributes alike — now returns the decoded text rather than the raw markup; `RawValue` is the old behaviour. `AddChild` and `InsertChild` now indent the child they add; pass `indent: false` for the old behaviour. Both changes are silent at the call site, so it is worth grepping for them on upgrade. `XmlAttributeSyntax.Equals` and `ValueNode` are also nullable now - they always could be `null`, for an attribute written as a bare name, and the type now says so.

- Removed the interfaces `IXmlElement` and `IXmlElementSyntax`.  
  **Reason:** this made editing the syntax tree more difficult, as the interfaces had to be cast to the SyntaxNode constantly. As replacement a new class called `XmlElementBaseSyntax` was introduced.

- Added various enumerators for nodes, XML attributes and XML elements.  
  **Reason:** before the iterator methods were used, which generated a state machine and allocates memory. The enumerators are more efficient and don't allocate memory.

  The enumerators also have their own `First` and `FirstOrDefault`, because reaching the LINQ ones boxes the enumerator. They start a fresh walk rather than continuing from wherever the enumerator happens to sit, so `foreach`, `First` and `Count` mean the same thing whatever order they are called in — a struct enumerator that hands out itself otherwise gives the second reader the tail of the first one's walk.

- Improved `ReplaceNode` for XML elements.  
  **Reason:** Before a visitor was used to replace nodes, which allocated more memory and was less efficient.

- Values are escaped on the way in and decoded on the way out.  
  **Reason:** `SetAttribute` and the string-taking factories used to write their argument verbatim, so a value containing `&`, `<` or a quote produced a document that no longer parsed — and the caller had no way to opt in, because the parameter is a `string`. They now escape. In the other direction `Value` resolves the five entities XML predefines plus numeric character references, unwraps CDATA sections and skips comments; `RawValue` is the text exactly as the document has it.
  ```cs
  root = root.SetAttribute("Include", "A&B<C");   // Include="A&amp;B&lt;C"
  root.GetAttributeValue("Include");              // A&B<C
  root.GetAttribute("Include").RawValue;          // A&amp;B&lt;C
  ```
  Whitespace between an element's tags counts as its value, the way it does to an `XDocument` loaded with `LoadOptions.PreserveWhitespace` — this is a tree that keeps every character of the document, so discarding some of it here was never on offer. The scanner keeps whitespace that runs up to a tag as that tag's trivia rather than as content, so `WithText(" ")` used to write a document whose `Value` read back empty, and `<a>\n  <b>x</b>\n</a>` used to say its value was just `x`. `RawValue` covers the same range, which is exactly the text `ContentSpan` points at.

  Line endings follow the same rule a conforming reader does (XML 1.0 §2.11): a literal CRLF or lone CR in the document — in text, in whitespace between tags, or inside a CDATA section — reads back as one LF, while `&#xD;` is how a document says it means a carriage return and comes back as one. `RawValue` is untouched either way, and the escaping helpers write `&#xD;` so a value containing a CR survives the round trip.

  `XmlEscaping.EncodeText`, `XmlEscaping.EncodeAttributeValue`, `XmlEscaping.NormalizeLineEndings` and `XmlEscaping.Decode` are public for callers doing their own formatting. `Decode` resolves only what XML defines — an unrecognised reference such as `&nbsp;`, which has no meaning without a DTD, is left exactly as it was found rather than turned into a character the document does not contain.

  `SetAttribute` and `WithValue` also keep the quote character the attribute was already written with, and give a valueless attribute (`<a x />`, which an editor sees constantly) a real `=` rather than a value with nothing joining it to the name. `SetAttribute` reads a `:` in the name as a prefix, so `SetAttribute("xmlns:p", …)` finds its own attribute again on the next call instead of appending a duplicate, and it places a new attribute *in front of* one that is still being typed (`<a x= />`), which would otherwise swallow the value it was given.

- `AddChild` and `InsertChild` indent by default.  
  **Reason:** they used to weld the new child to its sibling, which turned a one-line diff into a reformatted line. The indent unit and the line ending are taken from what the document already does. Pass `indent: false` for the old behaviour.

- Added the following utility methods:
  - `GetOrAddElement` - gets or adds an element to the XML tree, with support for paths. For example:
    ```cs
    root = root.GetOrAddElement("Project/PropertyGroup", out var propertyGroup);
    ```
    A leading slash is accepted and ignored, so `"/Project/PropertyGroup"` means the same thing. Anything else that would produce a nameless segment - an empty path, a trailing slash, a doubled slash - throws `ArgumentException` rather than creating an element with no name. Because a creating path writes its segments into the document, they must also be names the document can read back: `GetOrAddElement("a b", …)` throws rather than producing `<a b />`, which comes back as an element `a` carrying an attribute `b` and so gets created again on every call. `GetElementsByPath` reads paths by the same rules, minus that last one - it writes nothing, so a segment no element can be named simply matches none.
  - `SetAttribute` - sets an attribute of an element. If the attribute does not exist, it is added.
    ```cs
    propertyGroup = propertyGroup.SetAttribute("TargetFramework", "net9.0");
    ```
  - `GetElement` / `GetElements` - the child elements with a given name, mirroring `GetAttribute` down to the optional prefix.
    ```cs
    XmlElementBaseSyntax propertyGroup = root.GetElement("PropertyGroup");

    foreach (XmlElementBaseSyntax reference in root.GetElements("PackageReference"))
    {
        // ...
    }
    ```
  - `GetElementsByPath` - every element reachable by a slash-separated child path. Unlike a hand-rolled walker, it expands *every* segment rather than taking the first match at each step, so a path crossing repeated ancestors sees all of them.
    ```cs
    foreach (XmlElementBaseSyntax ipSecurity in root.GetElementsByPath("location/system.webServer/security/ipSecurity"))
    {
        // one per <location>, not just the first
    }
    ```
  - `GetIndentUnit`, `GetIndent` and `GetNewLine` - what the document already does for formatting, so new nodes can be placed to match it without reimplementing `NormalizeTrivia`.
    ```cs
    string unit = root.GetIndentUnit();  // e.g. "    " or "\t"
    string newLine = root.GetNewLine();  // "\r\n" or "\n"
    ```
  - `Descendants` - every element below this one, name-filtered if you want, through a struct enumerator rather than `DescendantNodes().OfType<>()`.
    ```cs
    foreach (XmlElementBaseSyntax reference in root.Descendants("PackageReference"))
    {
        // including the ones inside Choose/When
    }
    ```
  - `GetElementByLocalName`, `GetElementsByLocalName`, `GetAttributeByLocalName`, `GetAttributeValueByLocalName` - match the local name whatever the prefix, for a document that is the same model whether or not it was hand-edited to use one.
  - A `StringComparison` on every name lookup - `GetElement`, `GetAttribute`, `Descendants` and the rest, though not the path APIs, which match ordinally - for the formats that are case-insensitive about names (MSBuild, `packages.config`). An empty prefix means "unprefixed", the same as `null`, so `GetAttributeValue("Type", string.Empty)` does what it looks like it does.
  - `Attributes` is now a struct enumerator over the attribute *nodes*, matching `Elements`, so the name, the value and the spans all stay reachable without allocating.
  - `ValueSpan` and `ContentSpan` - the span of an attribute value inside its quotes, and the range between an element's tags. Both hold up in a buffer being typed into, where the closing quote or end tag is synthesized and zero-width.
    ```cs
    TextSpan toReplace = attribute.ValueSpan;  // excludes the quotes
    ```
  - `NameSpan` - the span of an element's name: on the element itself, and on `StartTag` and `EndTag` separately, which together are the pair a rename or linked editing edits. Zero-width but positioned where the name goes for a tag still being typed.
    ```cs
    TextSpan hover = element.NameSpan;
    TextSpan renameSecondEnd = element.EndTag.NameSpan;
    ```
  - `TextSpan` deconstructs into `(start, length)`, so converting to another span type does not need to name this one - it shares its simple name with Roslyn's `TextSpan`, and a file naming both needs an alias. The optional `GerardSmit.Language.Xml.Roslyn` package goes further with `ToRoslynSpan()` and `ToXmlSpan()`; the core package stays dependency-free.
  - `GetOrAddElement` and `AddElement` take an optional predicate, so the first path segment can say *which* match it means.
    ```cs
    root = root.GetOrAddElement("PropertyGroup", g => g.GetAttribute("Condition") is null, out var group);
    ```
  - `WithText` sets an element's text content, escaped. `SyntaxFactory.XmlEmptyElement(name, attributes)` builds `<PackageReference Include="A" />` without touching tokens.
  - `GetElement`, `GetElements`, `GetElementsByPath`, `Descendants` and their `ByLocalName` counterparts also hang off `XmlDocumentSyntax`, treating the root element as the first path segment, so the nullable `Root` dance is gone from the common case.
    ```cs
    document.GetElementsByPath("Project/PropertyGroup/TargetFramework");
    ```
  - `SyntaxLocator.FindNode` answers with the node the caret is in at the end of the buffer, instead of falling back to the document.

**Original README:**
---

![logo image](http://neteril.org/~jeremie/language_xml_logo.png)

[![Build status](https://ci.appveyor.com/api/projects/status/5ur9sv9bp4nr7a3n?svg=true)](https://ci.appveyor.com/project/KirillOsenkov/xmlparser)
[![NuGet package](https://img.shields.io/nuget/v/GuiLabs.Language.Xml.svg)](https://nuget.org/packages/GuiLabs.Language.Xml)
[![NuGet package for VS Editor](https://img.shields.io/nuget/v/GuiLabs.Language.Xml.Editor.svg)](https://nuget.org/packages/GuiLabs.Language.Xml.Editor)

A Roslyn-inspired full-fidelity XML parser with no dependencies and a simple Visual Studio XML language service.

 * The parser produces a **full-fidelity** syntax tree, meaning every character of the source text is represented in the tree. The tree covers the entire source text.
 * The parser has **no dependencies** and can easily be made portable. I would appreciate a high quality pull request making the parser portable.
 * The parser is based on the section of the Roslyn VB parser that parses XML literals. The Roslyn code is ported to C# and is made standalone.
 * The parser is **error-tolerant**. It will still produce a full tree even from invalid XML with missing tags, extra invalid text, etc. Missing and skipped tokens are still represented in the tree.
 * The resulting tree is **immutable** and follows Roslyn's [green/red separation](https://blogs.msdn.microsoft.com/ericlippert/2012/06/08/persistence-facades-and-roslyns-red-green-trees/) for maximum reusability of nodes.
 * The parser has basic support for **incrementality**. Given a previous constructed tree and a list of changes it will try to reuse existing nodes and only re-create what is necessary.
 * This library is more **low-level** than XLinq (for instance XLinq doesn't seem to represent whitespace around attributes). Also it has no idea about XML namespaces and just tells you what's in the source text (whereas in XLinq there's too much ceremony around XML namespaces).

This is work in progress and by no means complete. Specifically:
 * XML DTD is not supported (Roslyn didn't support it either)
 * Code wasn't tuned for performance and allocations, I'm sure a lot can be done to reduce memory consumption by the resulting tree. It should be pretty efficient though.
 * We reserve the right to accept only very high quality pull requests. We have very limited time to work on this so I ask everybody to please respect that.

## Download from NuGet:
 * [GuiLabs.Language.Xml](https://www.nuget.org/packages/GuiLabs.Language.Xml)
 * [GuiLabs.Language.Xml.Editor](https://www.nuget.org/packages/GuiLabs.Language.Xml.Editor)

## Try it!

https://xmlsyntaxvisualizer.azurewebsites.net/index.html

The above app leverages the parser and can help you visualize the resulting syntax tree generated from an XML document.

Code is available at https://github.com/garuma/XmlSyntaxVisualizer
C# UWP example at https://github.com/michael-hawker/XmlSyntaxVisualizerUWP

Also see the blog post: 
https://blog.neteril.org/blog/2018/03/21/xml-parsing-roslyn/

Resources about Immutable Syntax Trees:
https://github.com/KirillOsenkov/Bliki/wiki/Roslyn-Immutable-Trees

## FAQ:

### How to find a node in the tree given a position in the source text?
https://github.com/KirillOsenkov/XmlParser/blob/master/src/Microsoft.Language.Xml/Utilities/SyntaxLocator.cs#L24

```
SyntaxLocator.FindNode(SyntaxNode node, int position);
```

### How to replace a node in the tree

```csharp
var original = """
               <Project Sdk="Microsoft.NET.Sdk">
                 <PropertyGroup>
                   <TargetFramework>net8.0</TargetFramework>
                 </PropertyGroup>
               </Project>
               """;

var expected = """
               <Project Sdk="Microsoft.NET.Sdk">
                 <PropertyGroup>
                   <TargetFramework>net9.0</TargetFramework>
                 </PropertyGroup>
               </Project>
               """;

XmlDocumentSyntax root = Parser.ParseText(original);
XmlElementSyntax syntaxToReplace = root
    .Descendants()
    .OfType<XmlElementSyntax>()
    .Single(n => n.Name == "TargetFramework");
SyntaxNode textSyntaxToReplace = syntaxToReplace.Content.Single();

XmlTextSyntax content = SyntaxFactory.XmlText(SyntaxFactory.XmlTextLiteralToken("net9.0", null, null));

root = root.ReplaceNode(textSyntaxToReplace, content);

Assert.Equal(expected, root.ToFullString());
```
