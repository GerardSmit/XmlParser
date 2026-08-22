# Using this library instead of XDocument — a guide for agents

This project (`GerardSmit.Language.Xml`, namespace `Microsoft.Language.Xml`) is a
Roslyn-style, **full-fidelity, immutable** XML syntax tree. It is a fork of
`GuiLabs.Language.Xml` that added a high-level convenience API, so most tasks you would
reach for `XDocument` for — or write helper methods for — already have a built-in method.

**Check this document before writing a helper method.** Lookup by name/path,
get-or-create, attribute set with escaping, auto-indented insertion, descendant walks and
format detection all exist.

## Mental model — the three things that differ from XDocument

1. **Every character is in the tree.** Whitespace, comments, quote styles and entity text
   are all preserved. `node.ToFullString()` reproduces the input exactly. Edits therefore
   produce **minimal diffs** — this is the main reason to use this library for editing
   files that humans also edit (`.csproj`, `App.config`, `web.config`, `.props`, …).

2. **The tree is immutable.** Every "mutating" method returns a *new* node; the old one is
   unchanged. Always reassign, and never mix nodes from before and after an edit:

   ```csharp
   root = root.SetAttribute("Sdk", "Microsoft.NET.Sdk");   // WRONG without the `root =`
   ```

   After an edit, references into the *old* tree are stale — re-find nodes in the new
   tree, or use the `out` parameters the API provides (see `GetOrAddElement` below).

3. **It is error-tolerant and layout-aware, not namespace-aware.** Any text parses to a
   full tree (missing tags become zero-width synthesized tokens). There is no
   `XNamespace`; names are prefix + local name as written in the source. Use the
   `*ByLocalName` methods to match regardless of prefix.

## API mapping: XDocument → this library

| XDocument / XElement | This library | Notes |
|---|---|---|
| `XDocument.Parse(text)` | `Parser.ParseText(text)` → `XmlDocumentSyntax` | Never throws on bad XML. |
| `XDocument.Load(path)` | `Parser.ParseText(File.ReadAllText(path))` | No file/stream API. |
| `doc.ToString()` / `doc.Save(...)` | `node.ToFullString()`, then `File.WriteAllText` | Exact text, no reformatting. |
| `doc.Root` | `doc.Root` (nullable) — or skip it, see below | Lookup methods exist on `XmlDocumentSyntax` directly. |
| `element.Name.LocalName` | `element.Name` (qualified) / `element.NameNode.LocalName` | |
| `element.Value` | `element.Value` | Decoded: entities resolved, CDATA unwrapped, comments skipped. `RawValue` = markup as written. |
| `element.SetValue(text)` | `element.WithText(text)` | Escapes `text`; returns new element. |
| `element.Element("Name")` | `element.GetElement("Name")` | Optional `prefix` and `StringComparison` args. |
| `element.Elements("Name")` | `element.GetElements("Name")` | Struct enumerator, allocation-free. |
| `element.Elements()` | `element.Elements` | All child elements. |
| `element.Descendants("Name")` | `element.Descendants("Name")` / `doc.Descendants("Name")` | On the document it includes the root. |
| `element.Attribute("n")?.Value` | `element.GetAttributeValue("n")` or `element["n"]` | Decoded; `null` when absent. |
| `element.Attribute("n")` | `element.GetAttribute("n")` → `XmlAttributeSyntax?` | |
| `element.Attributes()` | `element.Attributes` | Attribute *nodes*: name, value, spans. |
| `element.SetAttributeValue("n", v)` | `element.SetAttribute("n", v)` | Adds or replaces; escapes `v`; keeps existing quote style; understands `"xmlns:p"`-style names. Returns new element. |
| `element.Add(child)` | `element.AddChild(child)` | **Auto-indents by default** (see below). |
| insert at position | `element.InsertChild(child, index)` | Also auto-indents. |
| `child.Remove()` | `parent.RemoveChild(child)` or `root.RemoveNode(node, SyntaxRemoveOptions.KeepNoTrivia)` | Nodes don't remove themselves; call on the parent/root and keep the result. |
| `ReplaceWith` | `root.ReplaceNode(oldNode, newNode)` | Returns the new root. |
| `new XElement("n", new XAttribute("a","v"))` | `SyntaxFactory.XmlEmptyElement("n", SyntaxFactory.XmlAttribute("a", "v"))` | Builds `<n a="v" />`; values escaped. |
| XPath-ish `doc.XPathSelectElements("/a/b")` | `doc.GetElementsByPath("a/b")` | Child axis only; every segment expands (all matches, not the first). |
| — (no XDocument equivalent) | `element.GetOrAddElement("a/b", out var b)` | Get-or-create along a path. |
| — | `root.GetIndentUnit()`, `root.GetNewLine()`, `element.GetIndent()` | The document's own formatting conventions. |
| — | `attribute.ValueSpan`, `element.ContentSpan`, `element.NameSpan`, `SyntaxLocator.FindNode(node, position)` | Exact source spans / node at caret — XDocument cannot do this. |

## What this library does that XDocument cannot

### Auto-indentation on insert

`AddChild`, `InsertChild`, `AddElement` and `GetOrAddElement` place new elements on their
own line, indented one level, **copying the indent unit (spaces vs tabs) and line ending
(CRLF vs LF) from what the document already does**. You never need to build whitespace
trivia yourself. Pass `indent: false` to `AddChild`/`InsertChild` for verbatim placement.
For a hand-built subtree, `node.NormalizeTrivia(parent)` re-indents it recursively.

### Get-or-create by path

```csharp
XmlDocumentSyntax doc = Parser.ParseText(File.ReadAllText(path));
var root = (XmlElementSyntax)doc.Root!;

// Finds Project/PropertyGroup or creates the missing part, correctly indented.
root = root.GetOrAddElement("PropertyGroup", out var group);

// Choose WHICH match the first segment means (e.g. the unconditioned PropertyGroup):
root = root.GetOrAddElement("PropertyGroup", g => g.GetAttribute("Condition") is null, out group);

// Always-create variant, resolving parent segments get-or-add style:
root = root.AddElement("ItemGroup/PackageReference", out var reference);

doc = doc.ReplaceNode(doc.Root!, root);
File.WriteAllText(path, doc.ToFullString());
```

The `out` parameter is the node **inside the returned tree** — safe to keep editing.
Paths reject empty segments and (for the creating APIs) invalid names with
`ArgumentException`; a leading `/` is allowed and ignored.

### Escaping handled for you

`SetAttribute`, `WithValue`, `WithText`, `SyntaxFactory.XmlAttribute(name, value)` and
`SyntaxFactory.XmlEmptyElement(name, attrs)` escape their string arguments; `Value` /
`GetAttributeValue` decode on the way out (the five XML entities, numeric character
references, CDATA). Never pre-escape a value you pass in, and never decode a `Value` you
read. `RawValue` is the raw markup when you need it. For custom formatting,
`XmlEscaping.EncodeText` / `EncodeAttributeValue` / `Decode` / `NormalizeLineEndings` are
public.

### Editor-grade robustness

The tree survives half-written XML (`<a x= />`, unclosed quotes, missing end tags), and
the editing APIs are written for it: `SetAttribute` won't duplicate or corrupt attributes
in a broken tag, spans stay valid with zero-width synthesized tokens, and
`SyntaxLocator.FindNode(doc, position)` gives the node at a caret position.

### Exact source spans

`attribute.ValueSpan` (inside the quotes), `element.ContentSpan` (between the tags) and
`element.NameSpan` (the name in the start tag) give editor-ready ranges; for a rename or
linked editing, `element.StartTag.NameSpan` and `element.EndTag.NameSpan` are the pair.
All of them stay positioned (zero-width) while the construct is still being typed. This
library's `TextSpan` shares its simple name with Roslyn's — either alias one of the two,
use `var (start, length) = span;` (it deconstructs), or reference the optional
`GerardSmit.Language.Xml.Roslyn` package for `span.ToRoslynSpan()` / `roslynSpan.ToXmlSpan()`.

### Case-insensitive and prefix-agnostic lookup

Every lookup takes an optional `StringComparison` (useful for MSBuild-style documents):
`root.GetElements("propertygroup", comparison: StringComparison.OrdinalIgnoreCase)`.
The `*ByLocalName` variants (`GetElementByLocalName`, `GetAttributeValueByLocalName`,
`DescendantsByLocalName`, …) match whatever the prefix — the closest thing to
namespace-blind matching. Prefix `null` and `""` both mean "no prefix".

## Common recipes

### Read values from a csproj

```csharp
XmlDocumentSyntax doc = Parser.ParseText(text);

string? tfm = doc.GetElementsByPath("Project/PropertyGroup/TargetFramework")
    .FirstOrDefault()?.Value;

foreach (XmlElementBaseSyntax pkg in doc.Descendants("PackageReference"))
{
    string? include = pkg.GetAttributeValue("Include");
    string? version = pkg.GetAttributeValue("Version");
}
```

Note: `doc.GetElementsByPath` treats the **root element as the first segment**, so the
path reads the way the file does — no `doc.Root` null-dance needed.

### Change an element's text

```csharp
var tfm = (XmlElementSyntax)doc.GetElementsByPath("Project/PropertyGroup/TargetFramework").First();
doc = doc.ReplaceNode(tfm, tfm.WithText("net9.0"));
```

### Add a package reference (minimal diff, correct indent)

```csharp
var root = (XmlElementSyntax)doc.Root!;
root = root.GetOrAddElement("ItemGroup", out var itemGroup);
root = root.ReplaceNode(itemGroup, ((XmlElementSyntax)itemGroup).AddChild(
    SyntaxFactory.XmlEmptyElement("PackageReference",
        SyntaxFactory.XmlAttribute("Include", "Newtonsoft.Json"),
        SyntaxFactory.XmlAttribute("Version", "13.0.3"))));
doc = doc.ReplaceNode(doc.Root!, root);
```

### Set / update an attribute

```csharp
var element = root.GetElement("PackageReference")!;
root = root.ReplaceNode(element, element.SetAttribute("Version", "13.0.3"));
```

### Remove an element without leaving a blank line

```csharp
root = root.RemoveNode(oldReference, SyntaxRemoveOptions.KeepNoTrivia);
```

## Gotchas checklist

- **Reassign every edit.** All edit methods return new nodes; nothing changes in place.
- **Stale references.** After `root = root.ReplaceNode(...)`, any node you found before
  the edit belongs to the old tree. Editing through it silently produces a tree that is
  not connected to `root`. Re-find, or use the `out` parameters.
- **`Value` includes whitespace** between tags (like `XDocument` with
  `LoadOptions.PreserveWhitespaces`): `<a>\n  <b>x</b>\n</a>` has value `"\n  x\n"`.
  Trim if you want the trimmed text.
- **`Value` is decoded, `RawValue` is not** (this changed in 3.0.0). Attribute nodes have
  the same pair, plus `ValueSpan` for the range inside the quotes.
- **This document describes 3.x.** The published 2.1.0 neither escapes on write
  (`SetAttribute` with `&`, `<` or `"` produces a document that no longer parses) nor
  decodes on read (`Value` returns raw markup). On 2.1.0, escape values yourself — and
  remove that escaping when upgrading, or values double-escape.
- **`XmlAttributeSyntax.ValueNode` and `.Equals` are nullable** — a bare-name attribute
  (`<a x />`) has neither. Prefer `GetAttributeValue`, which handles it.
- **No validation, no namespaces, no DTD.** Parsing never fails; check
  `doc.ContainsDiagnostics` / `node.GetDiagnostics()` if well-formedness matters.
- **Two element node types.** `<a/>` is `XmlEmptyElementSyntax`, `<a></a>` is
  `XmlElementSyntax`; code against their shared base `XmlElementBaseSyntax` (which is
  what all lookup APIs return). Only `XmlElementSyntax` has `Content`-carrying tags to
  put children into — `AddChild` on a parent works either way and returns
  `XmlElementSyntax`.
- **Don't use LINQ on the enumerators when you can avoid it.** `Elements`, `Attributes`,
  `Descendants(...)`, `GetElements(...)` are allocation-free struct enumerators with
  their own `First()` / `FirstOrDefault()`. Reaching LINQ's versions boxes them; it
  works, but prefer the built-ins or `foreach`.
- **Incremental reparse exists**: `Parser.ParseIncremental(newText, changes, previousTree)`
  reuses unchanged nodes — useful in an editor loop, unnecessary for one-shot edits.
