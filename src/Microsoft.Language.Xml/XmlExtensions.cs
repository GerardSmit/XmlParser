using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Language.Xml.Collections;

namespace Microsoft.Language.Xml
{
    public static class XmlExtensions
    {
        /// <summary>
        /// Returns the text content of a element node
        /// </summary>
        /// <remarks>
        /// Kept for callers that already use it; <see cref="XmlElementBaseSyntax.Value"/> now
        /// unwraps CDATA sections itself, so the two are the same thing.
        /// </remarks>
        public static string GetContentValue(this XmlElementBaseSyntax element)
        {
            return element.Value;
        }

        /// <summary>
        /// Return a new <see cref="XmlElementBaseSyntax"/> instance with
        /// the supplied string prefix.
        /// </summary>
        public static T WithPrefixName<T>(this T element, string prefixName)
            where T : XmlElementBaseSyntax
        {
            var existingName = element.NameNode;
            var existingPrefix = existingName.PrefixNode;
            Debug.Assert(existingPrefix != null);
            var newName = SyntaxFactory.XmlNameToken(prefixName, null, null);

            return (T)element.WithName(existingName.WithPrefix(existingPrefix.WithName(newName)));
        }

        public static T WithAttribute<T>(this T element, SyntaxList<XmlAttributeSyntax> newAttributes)
            where T : XmlElementBaseSyntax
        {
            return (T)element.WithAttributes(newAttributes);
        }

        public static T WithName<T>(this T element, XmlNameSyntax newName)
            where T : XmlElementBaseSyntax
        {
            return (T)element.WithName(newName);
        }

        /// <summary>
        /// Return a new <see cref="XmlAttributeSyntax"/> instance with the supplied string
        /// attribute value, escaped and kept inside the quote character the attribute already used.
        /// </summary>
        public static XmlAttributeSyntax WithValue(this XmlAttributeSyntax attribute, string attributeValue)
        {
            XmlStringSyntax? valueNode = attribute.ValueNode;
            PunctuationSyntax? startQuote = valueNode?.StartQuoteToken;

            // The quote character the attribute already uses is kept, so replacing a value does not
            // rewrite the quotes around it.
            var quote = startQuote is { Width: > 0 } ? startQuote.Text[0] : '"';

            SyntaxList<SyntaxNode> textTokens = SyntaxFactory.SingletonList<SyntaxNode>(
                SyntaxFactory.XmlTextLiteralToken(XmlEscaping.EncodeAttributeValue(attributeValue, quote), null, null));

            // Whether there is a value to replace is a question about widths, not about null: for
            // "<a x />" the parser answers with synthesized, zero-width "=" and string nodes, so
            // only an attribute built by hand has a genuinely absent one. When all three are really
            // there the value alone is swapped, which leaves the "=" and the quotes - and any line
            // break around them - exactly where the document put them.
            if (attribute.Equals is { Width: > 0 }
                && startQuote is { Width: > 0 }
                && valueNode!.EndQuoteToken is { Width: > 0 })
            {
                return attribute.WithValue(valueNode.WithTextTokens(textTokens));
            }

            // Anything else is half-written - a bare name, a lone "=", an unclosed quote - and is
            // completed rather than added to: giving an attribute a value without the "=" or the
            // closing quote produces a document that no longer parses.
            PunctuationSyntax newStartQuote = startQuote is { Width: > 0 }
                ? startQuote
                : SyntaxFactory.Punctuation(SyntaxKind.DoubleQuoteToken, "\"", null, null);

            XmlStringSyntax newValue = SyntaxFactory.XmlString(
                newStartQuote,
                textTokens,
                SyntaxFactory.Punctuation(newStartQuote.Kind, newStartQuote.Text, null, null));

            // Whatever followed the name, or the lone "=", is what separates this attribute from the
            // next one or from the closing ">", so it has to end up after the value instead.
            XmlNameSyntax name = attribute.NameNode;

            SyntaxTriviaList separator = attribute.Equals is { Width: > 0, HasTrailingTrivia: true } equals
                ? equals.GetTrailingTrivia()
                : name.GetTrailingTrivia();

            if (separator.Count > 0)
            {
                newValue = newValue.WithTrailingTrivia(separator);
            }

            return attribute.Update(
                name.WithTrailingTrivia(),
                SyntaxFactory.Punctuation(SyntaxKind.EqualsToken, "=", null, null),
                newValue);
        }

        public static XmlAttributeSyntax WithPrefixName(this XmlAttributeSyntax attribute, string prefixName)
        {
            var existingName = attribute.NameNode;
            var existingPrefix = existingName.PrefixNode;
            Debug.Assert(existingPrefix != null);
            var newName = SyntaxFactory.XmlNameToken(prefixName, null, null);

            return attribute.WithName(existingName.WithPrefix(existingPrefix.WithName(newName)));
        }

        public static XmlAttributeSyntax WithLocalName(this XmlAttributeSyntax attribute, string localName)
        {
            var existingName = attribute.NameNode;
            var existingLocalName = existingName.LocalNameNode;
            var newName = SyntaxFactory.XmlNameToken(localName, null, null);

            return attribute.WithName(existingName.WithLocalName(newName));
        }

        /// <summary>
        /// Appends a child element.
        /// </summary>
        /// <param name="indent">
        /// When <c>true</c> - the default - the child is placed on its own line, indented one level
        /// past the parent, taking the unit and the line ending from what the document already does.
        /// Pass <c>false</c> to add the child exactly as given, welded to whatever precedes it.
        /// </param>
        public static XmlElementSyntax AddChild<TSelf>(this TSelf parent, XmlElementBaseSyntax child, bool indent = true)
            where TSelf : XmlElementBaseSyntax
        {
            return AddChildCore(parent, child, index: -1, indent, out _);
        }

        /// <inheritdoc cref="AddChild{TSelf}(TSelf, XmlElementBaseSyntax, bool)"/>
        public static XmlElementSyntax AddChild<TSelf, TChild>(this TSelf parent, TChild child, out int index, bool indent = true)
            where TSelf : XmlElementBaseSyntax
            where TChild : XmlElementBaseSyntax
        {
            return AddChildCore(parent, child, index: -1, indent, out index);
        }

        /// <summary>
        /// Inserts a child element at the given position in the parent's content, or appends it
        /// when the index is -1.
        /// </summary>
        /// <inheritdoc cref="AddChild{TSelf}(TSelf, XmlElementBaseSyntax, bool)"/>
        public static XmlElementSyntax InsertChild<TSelf>(this TSelf parent, XmlElementBaseSyntax child, int index, bool indent = true)
            where TSelf : XmlElementBaseSyntax
        {
            return AddChildCore(parent, child, index, indent, out _);
        }

        private static XmlElementSyntax AddChildCore<TSelf>(TSelf parent, XmlElementBaseSyntax child, int index, bool indent, out int addedIndex)
            where TSelf : XmlElementBaseSyntax
        {
            if (indent)
            {
                AddLeadingTrivia(parent, ref child);

                // The child may carry the placeholder line break AddElementCore leaves on a
                // subtree built while detached. It has a real position now, so its own end tag
                // can finally be put under its start tag.
                if (child is XmlElementSyntax childElement)
                {
                    RealignEndTag(ref childElement);
                    child = childElement;
                }
            }

            XmlElementSyntax newParent;

            if (index < 0)
            {
                newParent = parent.WithContent(parent.Content.Add(child, out addedIndex));
            }
            else
            {
                newParent = parent.WithContent(parent.Content.Insert(index, child));
                addedIndex = index;
            }

            // A child on its own line needs the end tag on one too, or the tag ends up trailing the
            // child it now sits under.
            if (indent && StartsOnItsOwnLine(child))
            {
                EnsureRootTrivia(ref newParent);
            }

            return newParent;
        }

        /// <summary>
        /// Replaces an element's content with a single piece of text. The text is escaped, so any
        /// string can be passed without the result ceasing to be well-formed XML.
        /// </summary>
        /// <remarks>
        /// All of it, including the whitespace the scanner keeps as the end tag's trivia rather
        /// than as content. That whitespace is part of what the element says, so leaving it behind
        /// would make <c>WithText("t")</c> on an indented element read back as "t" plus whatever
        /// used to sit in front of the closing tag.
        /// </remarks>
        public static XmlElementSyntax WithText<TSelf>(this TSelf element, string text)
            where TSelf : XmlElementBaseSyntax
        {
            XmlElementSyntax result = element.WithContent(SyntaxFactory.SingletonList<SyntaxNode>(
                SyntaxFactory.XmlText(SyntaxFactory.List<SyntaxNode>(
                    SyntaxFactory.XmlTextLiteralToken(XmlEscaping.EncodeText(text), null, null)))));

            return result.EndTag is { } endTag && endTag.HasLeadingTrivia
                ? result.WithEndTag(endTag.WithoutLeadingTrivia())
                : result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static XmlElementSyntax RemoveChild<TSelf>(this TSelf parent, XmlElementBaseSyntax child)
            where TSelf : XmlElementBaseSyntax
        {
            return parent.WithContent(parent.Content.Remove(child));
        }

        internal static bool IsXmlNodeName(this XmlNameSyntax name)
        {
            var p = name.Parent;
            if (p == null) return false;
            switch (p.Kind)
            {
                case SyntaxKind.XmlElement:
                case SyntaxKind.XmlEmptyElement:
                case SyntaxKind.XmlElementStartTag:
                case SyntaxKind.XmlElementEndTag:
                    return true;
                default: return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AddAttributes<T>(this T self, params XmlAttributeSyntax[] attributes)
            where T : XmlElementBaseSyntax
        {
            return (T)self.WithAttributes(self.AttributesNode.AddRange(attributes));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AddAttributes<T>(this T self, IEnumerable<XmlAttributeSyntax> attributes)
            where T : XmlElementBaseSyntax
        {
            return (T)self.WithAttributes(self.AttributesNode.AddRange(attributes));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AddAttribute<T>(this T self, XmlAttributeSyntax attribute)
            where T : XmlElementBaseSyntax
        {
            return (T)self.WithAttributes(self.AttributesNode.Add(attribute));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T RemoveAttribute<T>(this T self, XmlAttributeSyntax attribute)
            where T : XmlElementBaseSyntax
        {
            return (T)self.WithAttributes(self.AttributesNode.Remove(attribute));
        }

        /// <summary>
        /// Sets an attribute, adding it when it is not there yet. The value is escaped, so any
        /// string can be passed without the result ceasing to be well-formed XML.
        /// </summary>
        public static T SetAttribute<T>(this T element, string attributeName, string value)
            where T : XmlElementBaseSyntax
        {
            // "xmlns:p" names a prefix and a local name, and the document will read it back that
            // way. Looking it up as one flat local name would never find what this method just
            // wrote, so a second call would append a duplicate rather than replace it.
            var colon = attributeName.IndexOf(':');
            var prefix = colon < 0 ? null : attributeName.Substring(0, colon);
            var localName = colon < 0 ? attributeName : attributeName.Substring(colon + 1);

            XmlNameMatcher.Validate(prefix, attributeName, nameof(attributeName));
            XmlNameMatcher.Validate(localName, attributeName, nameof(attributeName));

            XmlAttributeSyntax? attribute = element.GetAttribute(localName, prefix);

            // Setting the value of an attribute that is already there is exactly what WithValue
            // does, and going through it is what keeps the two from drifting apart on the awkward
            // shapes - a bare name, a lone "=", an attribute sitting on a line of its own.
            if (attribute is not null)
            {
                return element.ReplaceNode(attribute, attribute.WithValue(value));
            }

            PunctuationSyntax startQuote = SyntaxFactory.Punctuation(SyntaxKind.DoubleQuoteToken, "\"", null, null);

            XmlStringSyntax newValue = SyntaxFactory.XmlString(
                startQuote,
                SyntaxFactory.List([
                    SyntaxFactory.XmlTextLiteralToken(XmlEscaping.EncodeAttributeValue(value, '"'), null, null)
                ]),
                SyntaxFactory.Punctuation(startQuote.Kind, startQuote.Text, null, null)
            );

            // An attribute still being typed - "a=" with nothing after it, or a quote that was
            // never closed - swallows whatever follows it, up to and sometimes past the end of its
            // own tag. What the parser then reports as the attributes after it is really one run of
            // stolen text, and there is no position inside that run to write at. So a tag holding
            // one goes back to the only position that is certainly inside it and certainly clear of
            // the run: right after the element name, leaving the broken attributes as they were.
            SyntaxList<XmlAttributeSyntax> attributes = element.AttributesNode;
            var index = attributes.Count;

            foreach (XmlAttributeSyntax existing in attributes)
            {
                if (IsHalfWritten(existing))
                {
                    index = 0;
                    break;
                }
            }

            // Whatever currently sits between the thing the new attribute follows and what comes
            // next is the separator this tag uses. The new attribute takes over the job: without
            // it the tag closes straight after the quote, and with an invented one a tag that had
            // none grows a space it never had.
            SyntaxTriviaList separator = index > 0
                ? Separator(attributes[index - 1])
                : element.NameNode.GetTrailingTrivia();

            var spaceBeginning = separator.Count == 0;

            if (separator.Count > 0)
            {
                newValue = newValue.WithTrailingTrivia(SyntaxFactory.Space);
            }

            XmlNameSyntax name = SyntaxFactory.XmlName(
                prefix is null
                    ? null
                    : SyntaxFactory.XmlPrefix(
                        SyntaxFactory.XmlNameToken(prefix, spaceBeginning ? SyntaxFactory.Space : null, null),
                        SyntaxFactory.Punctuation(SyntaxKind.ColonToken, ":", null, null)),
                SyntaxFactory.XmlNameToken(
                    localName,
                    spaceBeginning && prefix is null ? SyntaxFactory.Space : null,
                    null));

            return (T)element.WithAttributes(attributes.Insert(
                index,
                SyntaxFactory.XmlAttribute(
                    name,
                    SyntaxFactory.Punctuation(SyntaxKind.EqualsToken, "=", null, null),
                    newValue)));
        }

        /// <summary>
        /// Whether the attribute is one an editor is in the middle of typing: an "=" with no closed
        /// value after it, or one whose value ran on far enough to eat its own tag's closing token.
        /// A bare name is neither - it is a shape XML has no use for, but the parser reads it
        /// unambiguously and whatever is put after it stays where it is put.
        /// </summary>
        private static bool IsHalfWritten(XmlAttributeSyntax attribute)
        {
            // A bare name is not half-written: XML has no use for it, but the parser reads it
            // unambiguously and whatever is put after it stays where it is put.
            if (attribute.Equals is not { Width: > 0 } && attribute.ValueNode is not { Width: > 0 })
            {
                return false;
            }

            // A value with a quote at each end stopped where it was told to. It cannot have run
            // past the end of its own tag, whatever it holds in between - an unescaped ">" is
            // perfectly legal in an attribute value and says nothing about the tag being broken.
            return attribute.ValueNode is not { StartQuoteToken.Width: > 0, EndQuoteToken.Width: > 0 };
        }

        /// <summary>
        /// What separates this attribute from whatever follows it. An attribute written as a bare
        /// name has nothing after its name to hold that separator, so the name keeps it - but only
        /// then: for anything with an "=" the name's trailing space separates it from that "=", and
        /// taking it for the one after the attribute welds the next attribute to this one's quote.
        /// </summary>
        private static SyntaxTriviaList Separator(XmlAttributeSyntax attribute)
        {
            if (attribute.HasTrailingTrivia)
            {
                return attribute.GetTrailingTrivia();
            }

            return attribute.Equals is { Width: > 0 } || attribute.ValueNode is { Width: > 0 }
                ? default
                : attribute.NameNode.GetTrailingTrivia();
        }

        /// <summary>
        /// Gets, or creates, the element at the given slash-separated child path, resolving each
        /// segment to the first match and creating the ones that are missing.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// The path is empty or contains an empty segment. Rejecting these is what keeps a stray
        /// slash from materialising an element with no name.
        /// </exception>
        public static XmlElementSyntax GetOrAddElement(this XmlElementBaseSyntax root, string name, out XmlElementBaseSyntax result, Func<XmlElementBaseSyntax, XmlElementBaseSyntax, XmlElementBaseSyntax>? configure = null)
        {
            return root.GetOrAddElement(name, match: null, out result, configure);
        }

        /// <summary>
        /// Gets, or creates, the element at the given path, considering only elements
        /// <paramref name="match"/> accepts.
        /// </summary>
        /// <param name="match">
        /// Applied to the candidates for the <em>first</em> segment only. Deeper segments are
        /// resolved by name as usual - a predicate written about one level would otherwise filter
        /// every other level too, and a get-or-add that rejects the leaf it just created adds a
        /// duplicate on every call. Without it the first element with the right name wins, which in
        /// a real project file is often the conditioned one rather than the one the caller meant.
        /// </param>
        /// <inheritdoc cref="GetOrAddElement(XmlElementBaseSyntax, string, out XmlElementBaseSyntax, Func{XmlElementBaseSyntax, XmlElementBaseSyntax, XmlElementBaseSyntax})"/>
        public static XmlElementSyntax GetOrAddElement(this XmlElementBaseSyntax root, string name, Func<XmlElementBaseSyntax, bool>? match, out XmlElementBaseSyntax result, Func<XmlElementBaseSyntax, XmlElementBaseSyntax, XmlElementBaseSyntax>? configure = null)
        {
            name = NormalizePath(name, nameof(name), out var segmentCount);

            if (segmentCount > 1)
            {
                var parts = name.Split('/');
                return GetOrAddByPath(parts, root, match, out result, configure);
            }

            return root.GetOrAddElementCore(name, match, out result, configure).Node;
        }

        /// <inheritdoc cref="GetOrAddElement(XmlElementBaseSyntax, string, out XmlElementBaseSyntax, Func{XmlElementBaseSyntax, XmlElementBaseSyntax, XmlElementBaseSyntax})"/>
        public static XmlElementSyntax AddElement(this XmlElementBaseSyntax root, string name, out XmlElementBaseSyntax result, Func<XmlElementBaseSyntax, XmlElementBaseSyntax, XmlElementBaseSyntax>? configure = null)
        {
            return root.AddElement(name, match: null, out result, configure);
        }

        /// <summary>
        /// Adds an element at the given path, resolving the path's parent segments the way
        /// <see cref="GetOrAddElement(XmlElementBaseSyntax, string, Func{XmlElementBaseSyntax, bool}, out XmlElementBaseSyntax, Func{XmlElementBaseSyntax, XmlElementBaseSyntax, XmlElementBaseSyntax})"/>
        /// does, and always creating the leaf.
        /// </summary>
        public static XmlElementSyntax AddElement(this XmlElementBaseSyntax root, string name, Func<XmlElementBaseSyntax, bool>? match, out XmlElementBaseSyntax result, Func<XmlElementBaseSyntax, XmlElementBaseSyntax, XmlElementBaseSyntax>? configure = null)
        {
            name = NormalizePath(name, nameof(name), out var segmentCount);

            if (segmentCount > 1)
            {
                var parts = name.Split('/');
                var parentPath = new ArraySegment<string>(parts, 0, parts.Length - 1);
                XmlElementSyntax newRoot = GetOrAddByPath(parentPath, root, match, out XmlElementBaseSyntax parent, configure);
                var (newParent, index) = parent.AddElementCore(parts[parts.Length - 1], out result, configure);

                // The element AddElementCore handed back belongs to newParent, which is about to
                // be replaced by a copy of itself inside the returned tree. Handing that one out
                // gives the caller a node whose parent chain stops short of the document, so
                // every edit made through it is dropped without a word. Fetch it back out of the
                // tree that is actually returned.
                var path = new List<int>();

                if (!newRoot.TryReplaceXmlNode(parent, newParent, out XmlElementSyntax? replaced, path))
                {
                    throw new InvalidOperationException();
                }

                path.Add(index);
                result = replaced.GetElementByPath(path);

                return replaced;
            }

            return root.AddElementCore(name, out result, configure).Node;
        }

        /// <summary>
        /// Validates a path and drops the leading slash, so the rest of the method only ever sees
        /// segments it can use as names.
        /// </summary>
        /// <remarks>
        /// Only the creating APIs come through here, so the segments are checked as names too:
        /// what they name is about to be written into the document, and a segment the document
        /// would read back as something else makes a get-or-add that never gets.
        /// </remarks>
        private static string NormalizePath(string path, string paramName, out int segmentCount)
        {
            var start = XmlPath.Validate(path, paramName, out segmentCount, validateNames: true);

            return start == 0 ? path : path.Substring(start);
        }

        private static XmlElementSyntax GetOrAddByPath<T>(T parts, XmlElementBaseSyntax root, Func<XmlElementBaseSyntax, bool>? match, out XmlElementBaseSyntax result, Func<XmlElementBaseSyntax, XmlElementBaseSyntax, XmlElementBaseSyntax>? configure)
            where T : IList<string>
        {
            var i = 0;

            var path = new List<int>();
            XmlElementSyntax parent = root.GetOrAddElementCore(parts[i], match, out result, configure).Node;

            while (++i < parts.Count)
            {
                var (next, changed, index) = result.GetOrAddElementCore(parts[i], match: null, out XmlElementBaseSyntax nextResult, configure);

                if (changed)
                {
                    path.Clear();

                    if (!parent.TryReplaceXmlNode(result, next, out var newParent, path))
                    {
                        throw new InvalidOperationException();
                    }

                    parent = newParent;

                    path.Add(index);
                    result = parent.GetElementByPath(path);
                }
                else
                {
                    result = nextResult;
                }
            }

            return parent;
        }

        private static (XmlElementSyntax Node, bool Changed, int Index) GetOrAddElementCore(this XmlElementBaseSyntax root, string name, Func<XmlElementBaseSyntax, bool>? match, out XmlElementBaseSyntax result, Func<XmlElementBaseSyntax, XmlElementBaseSyntax, XmlElementBaseSyntax>? configure)
        {
            SyntaxList<SyntaxNode>.Enumerator enumerator = root.Content.GetEnumerator();

            while (enumerator.MoveNext())
            {
                if (enumerator.Current is not XmlElementBaseSyntax child)
                {
                    continue;
                }

                if (name.Equals(child.Name, StringComparison.Ordinal) && (match is null || match(child)))
                {
                    // The match may be self-closing, so it is not necessarily an XmlElementSyntax.
                    // The root is, though: finding a child at all means it has content.
                    result = child;
                    return ((XmlElementSyntax)root, false, enumerator.CurrentIndex);
                }
            }

            var (newRoot, index) = root.AddElementCore(name, out result, configure);

            return (newRoot, true, index);
        }

        private static (XmlElementSyntax Node, int Index) AddElementCore(this XmlElementBaseSyntax root, string name, out XmlElementBaseSyntax result, Func<XmlElementBaseSyntax, XmlElementBaseSyntax, XmlElementBaseSyntax>? configure)
        {
            result = SyntaxFactory.XmlEmptyElement(
                SyntaxFactory.LessThan,
                SyntaxFactory.XmlName(name, SyntaxFactory.Space),
                default(SyntaxNode),
                SyntaxFactory.SlashGreaterThan
            );

            if (configure is not null)
            {
                result = configure(root, result);
            }

            // The trivia is calculated here rather than left to AddChild, because `configure` may
            // have replaced the element between the two.
            AddLeadingTrivia(root, ref result);
            XmlElementSyntax newRoot = root.AddChild(result, out var index, indent: false);

            if (StartsOnItsOwnLine(result))
            {
                EnsureRootTrivia(ref newRoot);
            }

            result = (XmlElementBaseSyntax) newRoot.Content[index];
            return (newRoot, index);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TSelf NormalizeTrivia<TSelf>(this TSelf node, XmlElementBaseSyntax? parent, int extra = 1)
            where TSelf : XmlElementBaseSyntax
        {
            if (parent != null)
            {
                AddLeadingTrivia(parent, ref node, extra);
            }

            if (node is XmlElementSyntax { Content: { Count: > 0 } content })
            {
                var newContent = new SyntaxListBuilder<SyntaxNode>(content.Count);

                foreach (SyntaxNode child in content)
                {
                    SyntaxNode newChild = child;

                    if (newChild is XmlElementBaseSyntax childElement)
                    {
                        // The insertion parent is deliberately passed down unchanged, and only the
                        // level count grows. It is the only node here whose position in the document
                        // is known: everything below is new, carries no indentation of its own to
                        // scale from, and is not attached yet, so GetDepth would under-count it.
                        newChild = NormalizeTrivia(childElement, parent, extra + 1);
                    }

                    newContent.Add(newChild);
                }

                node = (TSelf)(object)node.WithContent(newContent.ToList());
            }

            // The end tag comes after the content, in both senses: only once the children have
            // their trivia is it known whether this element is laid out as a block, and only a
            // block element wants its end tag on a line of its own. An element holding nothing
            // but text keeps its end tag where it is - whitespace in front of an end tag is part
            // of what the element says, and giving <b>text</b> a line break would change its
            // value, not its layout.
            if (node is XmlElementSyntax element && LastChildWithLeadingTrivia(element, ownLineOnly: true) is not null)
            {
                EnsureRootTrivia(ref element, realignIndented: true);
                node = (TSelf)(object)element;
            }

            return node;
        }

        private static void EnsureRootTrivia(ref XmlElementSyntax newRoot, bool realignIndented = false)
        {
            if (!newRoot.EndTag.HasLeadingTrivia)
            {
                if (newRoot.StartTag.HasLeadingTrivia)
                {
                    newRoot = newRoot.WithEndTag(newRoot.EndTag.WithLeadingTrivia(newRoot.StartTag.GetLeadingTrivia()));
                }
                else
                {
                    newRoot = newRoot.WithEndTag(
                        newRoot.EndTag.WithLeadingTrivia(SyntaxFactory.EndOfLineTrivia(newRoot.GetNewLine())));
                }

                return;
            }

            RealignEndTag(ref newRoot, realignIndented);
        }

        /// <summary>
        /// A lone line break in front of the end tag is what the branch above stamps on a subtree
        /// nobody has placed yet - a placeholder, correct only for an element at column zero. Once
        /// the start tag has been given a real line of its own, the end tag belongs directly under
        /// it; left as it was, the closing tag sits at column zero however deep the element went.
        /// Anything more than the bare line break is the document's own text - an indent someone
        /// wrote, whitespace that is part of the element's value - and stays exactly as found,
        /// unless <paramref name="realignIndented"/> says otherwise: NormalizeTrivia passes it,
        /// because its whole job is rewriting layout, and the indent in front of the end tag may
        /// itself be a guess - the one AddElement stamps while the subtree is still detached.
        /// </summary>
        private static void RealignEndTag(ref XmlElementSyntax element, bool realignIndented = false)
        {
            SyntaxTriviaList endTrivia = element.EndTag.GetLeadingTrivia();

            var isBareLineBreak = endTrivia.Count == 1 && endTrivia[0].Kind == SyntaxKind.EndOfLineTrivia;

            var isLineBreakAndIndent = endTrivia.Count == 2
                && endTrivia[0].Kind == SyntaxKind.EndOfLineTrivia
                && endTrivia[1].Kind == SyntaxKind.WhitespaceTrivia;

            if (!isBareLineBreak && !(realignIndented && isLineBreakAndIndent))
            {
                return;
            }

            SyntaxTrivia? newLine = null;
            SyntaxTrivia? indent = null;

            // The alignment to copy is the last line the start tag begins: its final line break
            // and whatever whitespace immediately follows it.
            foreach (SyntaxTrivia trivia in element.StartTag.GetLeadingTrivia())
            {
                switch (trivia.Kind)
                {
                    case SyntaxKind.EndOfLineTrivia:
                        newLine = trivia;
                        indent = null;
                        break;
                    case SyntaxKind.WhitespaceTrivia:
                        indent = trivia;
                        break;
                    default:
                        indent = null;
                        break;
                }
            }

            // A start tag that does not begin a line has no alignment to copy.
            if (newLine is null)
            {
                return;
            }

            if (indent is null)
            {
                // Both tags sit at column zero already; all that can disagree is the line-ending
                // flavour, where the placeholder is a guess and the start tag is the document.
                if (newLine.Text != endTrivia[0].Text)
                {
                    element = element.WithEndTag(element.EndTag.WithLeadingTrivia(newLine));
                }

                return;
            }

            element = element.WithEndTag(element.EndTag.WithLeadingTrivia(newLine, indent));
        }

        private static void AddLeadingTrivia<TSelf>(XmlElementBaseSyntax root, ref TSelf result, int extra = 1)
            where TSelf : XmlElementBaseSyntax
        {
            XmlElementBaseSyntax? sibling = LastChildWithLeadingTrivia(root, ownLineOnly: true);

            // A sibling is the most reliable answer there is: it shows exactly how far one level
            // indents at this spot, without depending on the node being attached to a document.
            // Scaling from the parent's own trivia cannot be used for that, because a node handed
            // back by an earlier edit is detached and reports a depth of zero.
            if (extra == 1 && sibling is not null)
            {
                result = result.WithLeadingTrivia(sibling.GetLeadingTrivia());
                return;
            }

            if (root.HasLeadingTrivia)
            {
                result = result.WithLeadingTrivia(CalculateNewTrivia(root, root.GetLeadingTrivia(), extra));
            }
            else if (sibling is not null)
            {
                // The sibling sits one level in, so it is the level count past *it* that is added -
                // otherwise a grandchild lands at its parent's indent.
                result = result.WithLeadingTrivia(CalculateNewTrivia(sibling, sibling.GetLeadingTrivia(), extra - 1));
            }
            else if (extra == 1 && LastChildWithLeadingTrivia(root, ownLineOnly: false) is { } inline)
            {
                // No child starts a line, so the element is laid out inline. Matching it means
                // whatever separates its children - a space - not a line break and an indent.
                result = result.WithLeadingTrivia(inline.GetLeadingTrivia());
            }
            else if (root.Parent is null)
            {
                // Nothing here says how far one level indents - no indented child, no indentation
                // on the root - so the document's own unit answers, the same as it does everywhere
                // else. The line ending is still the document's own wherever it has one.
                result = result.WithLeadingTrivia(
                    SyntaxFactory.EndOfLineTrivia(root.GetNewLine()),
                    SyntaxFactory.WhitespaceTrivia(Repeat(DocumentIndentUnit(root), extra)));
            }
        }

        /// <summary>
        /// The last child element with anything in front of it - restricted to those starting a
        /// line of their own when <paramref name="ownLineOnly"/> - which is the one that shows how
        /// this parent separates its children.
        /// </summary>
        /// <remarks>
        /// The last rather than the first, because the first is often welded to the start tag -
        /// <c>&lt;G&gt;&lt;A /&gt;</c> - and carries no indentation to copy, while a child added by
        /// an earlier edit in the same chain does.
        /// </remarks>
        private static XmlElementBaseSyntax? LastChildWithLeadingTrivia(XmlElementBaseSyntax root, bool ownLineOnly)
        {
            XmlElementBaseSyntax? found = null;

            foreach (XmlElementBaseSyntax element in root.Elements)
            {
                if (ownLineOnly ? StartsOnItsOwnLine(element) : element.HasLeadingTrivia)
                {
                    found = element;
                }
            }

            return found;
        }

        /// <summary>
        /// Whether the node begins a line, rather than merely having something in front of it. A
        /// child separated from its sibling by a single space is laid out inline, and copying that
        /// space as an indent would put the next child inline too while still claiming a new line.
        /// </summary>
        private static bool StartsOnItsOwnLine(SyntaxNode node)
        {
            foreach (SyntaxTrivia trivia in node.GetLeadingTrivia())
            {
                if (trivia.Kind == SyntaxKind.EndOfLineTrivia)
                {
                    return true;
                }
            }

            return false;
        }

        internal static SyntaxTriviaList CalculateNewTrivia(XmlElementBaseSyntax root, SyntaxTriviaList trivia, int extra = 1)
        {
            if (extra <= 0)
            {
                return trivia;
            }

            SyntaxTrivia last = trivia.Last();

            // Only a run of whitespace is an indent to scale from. A node sitting at column zero
            // ends its trivia with the line break itself, and treating that as the indent would
            // repeat the newline and leave a blank line behind.
            var indent = last.Kind == SyntaxKind.WhitespaceTrivia ? last.Text : string.Empty;
            var depth = GetDepth(root);

            // A detached node, or the document root, has no depth to divide by; a node at column
            // zero has nothing to divide; and an indent shorter than the node is deep divides to
            // nothing, which would leave the indent never growing at all. In each case the
            // document's own unit is what says how far one level indents.
            var unit = depth > 0 && indent.Length >= depth
                ? indent.Substring(0, indent.Length / depth)
                : DocumentIndentUnit(root);

            SyntaxTrivia whitespace = SyntaxFactory.WhitespaceTrivia(indent + Repeat(unit, extra));

            return indent.Length > 0 ? trivia.Replace(last, whitespace) : trivia.Add(whitespace);
        }

        /// <summary>
        /// The indent unit of the document <paramref name="node"/> belongs to. Asking the node
        /// itself is no good here: this is the path taken precisely when the node has no indented
        /// child to learn from, and its own children are all the public overload looks at.
        /// </summary>
        private static string DocumentIndentUnit(XmlElementBaseSyntax node)
        {
            XmlElementBaseSyntax current = node;

            while (current.ParentElement is { } parent)
            {
                current = parent;
            }

            return current.GetIndentUnit();
        }

        private static string Repeat(string unit, int times)
        {
            return string.Concat(Enumerable.Repeat(unit, Math.Max(times, 1)));
        }

        /// <summary>
        /// Every element reachable from <paramref name="node"/> by the given slash-separated path,
        /// in document order. Child axis only - no predicates, attributes or <c>//</c>.
        /// </summary>
        /// <remarks>
        /// Every segment is expanded, not just the last: a path through two <c>&lt;location&gt;</c>
        /// elements yields matches under both.
        /// </remarks>
        public static XmlPathElementEnumerator GetElementsByPath(this XmlElementBaseSyntax node, string path)
        {
            return new XmlPathElementEnumerator(node.Content, path);
        }

        /// <summary>
        /// Every element in the document reachable by the given path, with the root element as the
        /// first segment - so <c>document.GetElementsByPath("Project/PropertyGroup")</c> reads the
        /// way the file does.
        /// </summary>
        /// <inheritdoc cref="GetElementsByPath(XmlElementBaseSyntax, string)"/>
        public static XmlPathElementEnumerator GetElementsByPath(this XmlDocumentSyntax document, string path)
        {
            return new XmlPathElementEnumerator(RootContent(document), path);
        }

        /// <summary>
        /// The document's root element when it has the given name, or <c>null</c>.
        /// </summary>
        public static XmlElementBaseSyntax? GetElement(this XmlDocumentSyntax document, string localName, string? prefix = null, StringComparison comparison = StringComparison.Ordinal)
        {
            return document.GetElements(localName, prefix, comparison).FirstOrDefault();
        }

        /// <summary>
        /// The document's root element when it has the given name. A document has at most one, so
        /// this yields zero or one element; it exists so the document reads like an element.
        /// </summary>
        public static XmlNamedElementEnumerator GetElements(this XmlDocumentSyntax document, string localName, string? prefix = null, StringComparison comparison = StringComparison.Ordinal)
        {
            return new XmlNamedElementEnumerator(RootContent(document), localName, prefix, matchAnyPrefix: false, comparison);
        }

        /// <summary>
        /// The document's root element when its local name matches, whatever its prefix.
        /// </summary>
        public static XmlElementBaseSyntax? GetElementByLocalName(this XmlDocumentSyntax document, string localName, StringComparison comparison = StringComparison.Ordinal)
        {
            return document.GetElementsByLocalName(localName, comparison).FirstOrDefault();
        }

        /// <inheritdoc cref="GetElementByLocalName(XmlDocumentSyntax, string, StringComparison)"/>
        public static XmlNamedElementEnumerator GetElementsByLocalName(this XmlDocumentSyntax document, string localName, StringComparison comparison = StringComparison.Ordinal)
        {
            return new XmlNamedElementEnumerator(RootContent(document), localName, prefix: null, matchAnyPrefix: true, comparison);
        }

        /// <summary>
        /// Every element in the document, in document order, including the root.
        /// </summary>
        public static XmlDescendantElementEnumerator Descendants(this XmlDocumentSyntax document)
        {
            // Starting from the document's content rather than from the root element is what keeps
            // the root itself in the results.
            return new XmlDescendantElementEnumerator(RootContent(document));
        }

        /// <inheritdoc cref="Descendants(XmlDocumentSyntax)"/>
        public static XmlDescendantElementEnumerator Descendants(this XmlDocumentSyntax document, string? localName, string? prefix = null, StringComparison comparison = StringComparison.Ordinal)
        {
            return new XmlDescendantElementEnumerator(RootContent(document), localName, prefix, matchAnyPrefix: false, comparison);
        }

        /// <summary>
        /// Every element in the document whose local name matches, whatever its prefix.
        /// </summary>
        public static XmlDescendantElementEnumerator DescendantsByLocalName(this XmlDocumentSyntax document, string localName, StringComparison comparison = StringComparison.Ordinal)
        {
            return new XmlDescendantElementEnumerator(RootContent(document), localName, prefix: null, matchAnyPrefix: true, comparison);
        }

        /// <summary>
        /// The document body as a content list, so the root element is the first thing a path or a
        /// lookup is matched against.
        /// </summary>
        private static SyntaxList<SyntaxNode> RootContent(XmlDocumentSyntax document)
        {
            XmlElementBaseSyntax? root = document.Root;

            return root is null ? default : new SyntaxList<SyntaxNode>(root);
        }

        /// <summary>
        /// The whitespace one nesting level adds in this document, inferred from what the document
        /// root already contains. Falls back to two spaces when there is nothing to infer from.
        /// </summary>
        /// <param name="documentRoot">
        /// The document's root element. Its children sit exactly one level in, which is what makes
        /// their indentation a unit; passing a deeper element yields that element's total indent.
        /// </param>
        public static string GetIndentUnit(this XmlElementBaseSyntax documentRoot)
        {
            // The first child that starts a line of its own, not simply the first child: a document
            // whose root opens "<a><b>" says nothing with that first child, and answering with the
            // fallback would put spaces into a file written with tabs. A child laid out inline has
            // only a separator in front of it, which is not an indent however wide it is.
            foreach (XmlElementBaseSyntax child in documentRoot.Elements)
            {
                var unit = child.GetIndent();

                if (unit.Length > 0 && StartsOnItsOwnLine(child))
                {
                    return unit;
                }
            }

            return "  ";
        }

        /// <summary>
        /// The whitespace this element sits behind, or an empty string when it starts a line or is
        /// the document root.
        /// </summary>
        public static string GetIndent(this XmlElementBaseSyntax element)
        {
            return element.GetLeadingTrivia()
                .LastOrDefault(x => x.Kind == SyntaxKind.WhitespaceTrivia)?
                .Text ?? string.Empty;
        }

        /// <summary>
        /// The line ending this document uses, taken from the first one the parser produced.
        /// Falls back to <c>\r\n</c> for a document that contains no line break at all.
        /// </summary>
        public static string GetNewLine(this XmlElementBaseSyntax documentRoot)
        {
            // This element's own text comes first. It is what an edit here has to match, and asking
            // it first keeps the answer stable however much minified document sits in front of it.
            // Its end tag is read before the scan: a minified element keeps its only line break
            // right there, past where any bounded forward scan would reach, and it is one token.
            var newLine = FindNewLineAt((documentRoot as XmlElementSyntax)?.EndTag)
                ?? FindNewLine(documentRoot);

            if (newLine is not null)
            {
                return newLine;
            }

            SyntaxNode scope = documentRoot;

            while (scope.Parent is { } parent)
            {
                scope = parent;
            }

            if (!ReferenceEquals(scope, documentRoot))
            {
                // A minified file with a POSIX final newline keeps its only line break on the
                // document's very last token, past the root element's end tag - one token, and
                // worth reading directly rather than scanning the whole document to reach.
                newLine = FindNewLineAt((scope as XmlDocumentSyntax)?.Eof) ?? FindNewLine(scope);

                if (newLine is not null)
                {
                    return newLine;
                }
            }

            return SyntaxFactory.CarriageReturnLineFeed.Text;
        }

        /// <summary>
        /// The first line ending on a single node's own trivia, without descending into it.
        /// </summary>
        private static string? FindNewLineAt(SyntaxNode? node)
        {
            if (node is null)
            {
                return null;
            }

            return FindNewLine(node.GetLeadingTrivia()) ?? FindNewLine(node.GetTrailingTrivia());
        }

        private static string? FindNewLine(SyntaxNode scope)
        {
            var budget = ScanTokenLimit;

            foreach (SyntaxNode node in scope.DescendantNodesAndTokensAndSelf())
            {
                if (node.IsNode)
                {
                    continue;
                }

                var newLine = FindNewLine(node.GetLeadingTrivia())
                    ?? FindNewLine(((SyntaxToken)node).Text)
                    ?? FindNewLine(node.GetTrailingTrivia());

                if (newLine is not null)
                {
                    return newLine;
                }

                if (--budget == 0)
                {
                    break;
                }
            }

            return null;
        }

        /// <summary>
        /// How far <see cref="GetNewLine"/> reads into any one node before giving up. A stretch of
        /// a thousand tokens with no line break in it is minified for any purpose this serves, and
        /// reading further only costs time on exactly the documents that cannot answer.
        /// </summary>
        private const int ScanTokenLimit = 1000;

        private static string? FindNewLine(string text)
        {
            for (var i = 0; i < text.Length; i++)
            {
                switch (text[i])
                {
                    case '\n':
                        return "\n";

                    // A lone carriage return is a line ending in its own right, and the scanner
                    // reports one as such, so the two answers agree on a file written that way.
                    case '\r':
                        return i + 1 < text.Length && text[i + 1] == '\n'
                            ? SyntaxFactory.CarriageReturnLineFeed.Text
                            : "\r";
                }
            }

            return null;
        }

        private static string? FindNewLine(SyntaxTriviaList trivia)
        {
            foreach (SyntaxTrivia item in trivia)
            {
                if (item.Kind == SyntaxKind.EndOfLineTrivia)
                {
                    return item.Text;
                }
            }

            return null;
        }

        private static int GetDepth(XmlElementBaseSyntax root)
        {
            var depth = 0;
            XmlElementBaseSyntax current = root;

            while (current.ParentElement is { } parent)
            {
                current = parent;
                depth++;
            }

            return depth;
        }
    }
}
