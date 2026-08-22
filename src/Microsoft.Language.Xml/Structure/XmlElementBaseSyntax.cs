using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Language.Xml.Collections;

namespace Microsoft.Language.Xml
{
    public abstract class XmlElementBaseSyntax : XmlNodeSyntax, INamedXmlNode
    {
        internal XmlElementBaseSyntax(Green green, SyntaxNode? parent, int position) : base(green, parent, position)
        {
        }

        public abstract string Name { get; }

        /// <summary>
        /// The element's content exactly as it appears in the document, markup and all.
        /// </summary>
        public abstract string RawValue { get; }

        public abstract XmlNameSyntax NameNode { get; }
        public abstract SyntaxList<SyntaxNode> Content { get; }
        public XmlElementBaseSyntax AsElement => this;

        public new XmlElementBaseSyntax? Parent => base.Parent as XmlElementBaseSyntax;

        public abstract XmlElementEnumerator Elements { get; }

        public abstract SyntaxList<XmlAttributeSyntax> AttributesNode { get; }

        /// <summary>
        /// The element's attributes. Allocation-free when enumerated directly.
        /// </summary>
        public XmlAttributeNodeEnumerator Attributes => new XmlAttributeNodeEnumerator(AttributesNode);

        /// <summary>
        /// The element's text: entity references resolved, CDATA sections unwrapped, comments
        /// skipped, and the text of nested elements included in document order. This is the value a
        /// caller means when they ask what an element says; <see cref="RawValue"/> is the markup it
        /// is written with.
        /// </summary>
        /// <remarks>
        /// Whitespace between the tags counts, the way it does to an <c>XDocument</c> loaded with
        /// <c>LoadOptions.PreserveWhitespace</c> - this is a tree that keeps every character of
        /// the document, so throwing some away here is not on offer. The scanner keeps whitespace
        /// running up to a tag as that tag's trivia rather than as content, so it has to be asked
        /// for there; otherwise <c>WithText(" ")</c> writes a document whose value reads back
        /// empty.
        /// </remarks>
        public string Value
        {
            get
            {
                SyntaxList<SyntaxNode> content = Content;
                var trailing = XmlEscaping.NormalizeLineEndings(ContentTrailingTrivia);

                if (content.Count == 0)
                {
                    return trailing;
                }

                // The overwhelmingly common shape is a single text node, which needs no builder
                // and usually no decoding either.
                if (content.Count == 1 && content[0] is XmlTextSyntax onlyText && trailing.Length == 0)
                {
                    return XmlEscaping.Decode(onlyText.ToFullString());
                }

                var builder = new StringBuilder();
                AppendValue(content, builder);
                builder.Append(trailing);
                return builder.ToString();
            }
        }

        /// <summary>
        /// The text between the last thing an element holds and its end tag. Whitespace that runs
        /// up to a tag is kept as trivia rather than as content, so this is where it lives - and
        /// for an element holding nothing else, it is the whole of what the element says. Empty
        /// for one that has no end tag to run up to, which is every self-closing element.
        /// </summary>
        protected virtual string ContentTrailingTrivia => string.Empty;

        /// <summary>
        /// Walks the content in document order, descending through nested elements.
        /// </summary>
        /// <remarks>
        /// Descent is an explicit stack rather than recursion. How deep this goes is whatever the
        /// document says, and a parser that tolerates whatever a buffer holds should not answer a
        /// deeply nested one by taking the process down with it - the rest of the library already
        /// does not, the descendant enumerator included.
        /// </remarks>
        private static void AppendValue(SyntaxList<SyntaxNode> content, StringBuilder builder)
        {
            var stack = new Stack<Level>();
            stack.Push(new Level(content, string.Empty));

            while (stack.Count > 0)
            {
                Level level = stack.Peek();

                if (level.Index == level.Content.Count)
                {
                    // Everything this element holds has been written, so what runs up to its end
                    // tag comes last.
                    builder.Append(level.Trailing);
                    stack.Pop();
                    continue;
                }

                SyntaxNode node = level.Content[level.Index];
                level.Index++;

                switch (node)
                {
                    case XmlTextSyntax text:
                        builder.Append(XmlEscaping.Decode(text.ToFullString()));
                        break;
                    // Everything inside a CDATA section is literal by definition, so it is taken as
                    // it stands rather than decoded - but line endings are handled before any of
                    // that, so a CDATA section is no exception to them.
                    case XmlCDataSectionSyntax cdata:
                        AppendLeadingTrivia(cdata, builder);
                        builder.Append(XmlEscaping.NormalizeLineEndings(cdata.TextTokens.ToFullString()));
                        break;
                    case XmlElementBaseSyntax element:
                        AppendLeadingTrivia(element, builder);
                        stack.Push(new Level(
                            element.Content,
                            XmlEscaping.NormalizeLineEndings(element.ContentTrailingTrivia)));
                        break;
                    // Comments and processing instructions are markup about the document, not part
                    // of what it says - but the whitespace in front of one is text like any other,
                    // and dropping it makes a comment silently join the words either side of it.
                    default:
                        AppendLeadingTrivia(node, builder);
                        break;
                }
            }
        }

        /// <summary>
        /// One element's content, how far through it the walk is, and what it owes the builder
        /// once it is done.
        /// </summary>
        private sealed class Level
        {
            public Level(SyntaxList<SyntaxNode> content, string trailing)
            {
                Content = content;
                Trailing = trailing;
            }

            public SyntaxList<SyntaxNode> Content { get; }

            public string Trailing { get; }

            public int Index { get; set; }
        }

        /// <summary>
        /// The whitespace in front of a tag belongs to the text around it, but it is held inside
        /// the node it runs up to. Skipping it makes a parent disagree with its own children about
        /// what they say, and makes a comment silently join the words either side of it.
        /// </summary>
        private static void AppendLeadingTrivia(SyntaxNode node, StringBuilder builder)
        {
            builder.Append(XmlEscaping.NormalizeLineEndings(node.GetLeadingTrivia().ToFullString()));
        }

        /// <summary>
        /// The range between the start and end tags - where content goes. Empty, but positioned,
        /// for an element that has none, which is what a diagnostic about a missing value points at.
        /// </summary>
        public abstract TextSpan ContentSpan { get; }

        /// <summary>
        /// The span of the element's name in its start tag - what a rename selects, what a hover
        /// over the tag is about, and where a diagnostic about the element as a whole points.
        /// Empty, but positioned, for a tag whose name is still to be typed: the parser answers
        /// one of those with a zero-width name sitting exactly where the name would go. For the
        /// end tag's copy of the name, see <see cref="XmlElementEndTagSyntax.NameSpan"/>.
        /// </summary>
        public TextSpan NameSpan
        {
            get
            {
                // The parser never leaves the name out - a missing one is a zero-width node at
                // the right position - so the fallback only answers for a tree built by hand
                // with a null name, where the element's own start is all there is to point at.
                XmlNameSyntax? name = NameNode;

                return name is not null ? name.Span : new TextSpan(Span.Start, 0);
            }
        }

        public abstract XmlElementSyntax WithContent(SyntaxList<SyntaxNode> newContent);

        protected internal abstract XmlElementBaseSyntax WithAttributes(SyntaxList<XmlAttributeSyntax> newAttributes);

        protected internal abstract XmlElementBaseSyntax WithName(XmlNameSyntax newName);

        /// <summary>
        /// The first child element with the given name, or <c>null</c> when there is none.
        /// </summary>
        /// <param name="prefix">
        /// The namespace prefix the element must carry. <c>null</c> and the empty string both mean
        /// "no prefix"; use <see cref="GetElementByLocalName"/> to match regardless of prefix.
        /// </param>
        public XmlElementBaseSyntax? GetElement(string localName, string? prefix = null, StringComparison comparison = StringComparison.Ordinal)
        {
            return GetElements(localName, prefix, comparison).FirstOrDefault();
        }

        /// <summary>
        /// The child elements with the given name, in document order.
        /// </summary>
        /// <inheritdoc cref="GetElement"/>
        public XmlNamedElementEnumerator GetElements(string localName, string? prefix = null, StringComparison comparison = StringComparison.Ordinal)
        {
            return new XmlNamedElementEnumerator(Content, localName, prefix, matchAnyPrefix: false, comparison);
        }

        /// <summary>
        /// The first child element with the given local name whatever its prefix, or <c>null</c>
        /// when there is none.
        /// </summary>
        public XmlElementBaseSyntax? GetElementByLocalName(string localName, StringComparison comparison = StringComparison.Ordinal)
        {
            return GetElementsByLocalName(localName, comparison).FirstOrDefault();
        }

        /// <summary>
        /// The child elements with the given local name whatever their prefix, in document order.
        /// </summary>
        /// <remarks>
        /// A document that declares a default namespace reports bare names, but the same document
        /// hand-edited to use a prefix is still the same model - so a reader that cares about the
        /// model rather than the text matches on the local name alone.
        /// </remarks>
        public XmlNamedElementEnumerator GetElementsByLocalName(string localName, StringComparison comparison = StringComparison.Ordinal)
        {
            return new XmlNamedElementEnumerator(Content, localName, prefix: null, matchAnyPrefix: true, comparison);
        }

        /// <summary>
        /// Every element below this one, in document order, optionally filtered by name.
        /// </summary>
        public XmlDescendantElementEnumerator Descendants()
        {
            return new XmlDescendantElementEnumerator(Content);
        }

        /// <inheritdoc cref="Descendants()"/>
        /// <param name="localName">
        /// The local name to match, or <c>null</c> to match every name - which with a
        /// <paramref name="prefix"/> means every element in a given namespace prefix.
        /// </param>
        public XmlDescendantElementEnumerator Descendants(string? localName, string? prefix = null, StringComparison comparison = StringComparison.Ordinal)
        {
            return new XmlDescendantElementEnumerator(Content, localName, prefix, matchAnyPrefix: false, comparison);
        }

        /// <summary>
        /// Every element below this one whose local name matches, whatever its prefix.
        /// </summary>
        public XmlDescendantElementEnumerator DescendantsByLocalName(string localName, StringComparison comparison = StringComparison.Ordinal)
        {
            return new XmlDescendantElementEnumerator(Content, localName, prefix: null, matchAnyPrefix: true, comparison);
        }

        /// <inheritdoc cref="GetElement"/>
        public XmlAttributeSyntax? GetAttribute(string localName, string? prefix = null, StringComparison comparison = StringComparison.Ordinal)
        {
            foreach (XmlAttributeSyntax attr in AttributesNode)
            {
                if (XmlNameMatcher.Matches(attr.NameNode, localName, prefix, matchAnyPrefix: false, comparison))
                {
                    return attr;
                }
            }

            return null;
        }

        /// <summary>
        /// The first attribute with the given local name whatever its prefix.
        /// </summary>
        public XmlAttributeSyntax? GetAttributeByLocalName(string localName, StringComparison comparison = StringComparison.Ordinal)
        {
            foreach (XmlAttributeSyntax attr in AttributesNode)
            {
                if (XmlNameMatcher.Matches(attr.NameNode, localName, prefix: null, matchAnyPrefix: true, comparison))
                {
                    return attr;
                }
            }

            return null;
        }

        /// <summary>
        /// The decoded value of an attribute, or <c>null</c> when the element does not have it.
        /// </summary>
        /// <inheritdoc cref="GetElement"/>
        public string? GetAttributeValue(string localName, string? prefix = null, StringComparison comparison = StringComparison.Ordinal)
        {
            return GetAttribute(localName, prefix, comparison)?.Value;
        }

        /// <inheritdoc cref="GetAttributeByLocalName"/>
        public string? GetAttributeValueByLocalName(string localName, StringComparison comparison = StringComparison.Ordinal)
        {
            return GetAttributeByLocalName(localName, comparison)?.Value;
        }

        public string? this[string attributeName] => GetAttributeValue(attributeName);
    }
}
