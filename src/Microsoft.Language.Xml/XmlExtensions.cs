using System;
using System.Collections.Generic;
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
        /// In addition to the straightforward case
        /// of an element containing simple text tokens, this
        /// method also check for embedded CDATA sections
        /// </remarks>
        public static string GetContentValue(this XmlElementBaseSyntax element)
        {
            if (element.Content.Count == 1 && element.Content.First() is XmlCDataSectionSyntax cdata)
                return cdata.TextTokens.ToFullString();
            return element.AsElement.Value;
        }

        /// <summary>
        /// Return a new <see cref="IXmlElementSyntax"/> instance with
        /// the supplied string prefix.
        /// </summary>
        public static T WithPrefixName<T>(this T element, string prefixName)
            where T : XmlElementBaseSyntax
        {
            var existingName = element.NameNode;
            var existingPrefix = existingName.PrefixNode;
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
        /// Return a new <see cref="XmlAttributeSyntax"/> instance with
        /// the supplied string attribute value
        /// </summary>
        public static XmlAttributeSyntax WithValue(this XmlAttributeSyntax attribute, string attributeValue)
        {
            var textTokens = SyntaxFactory.SingletonList(SyntaxFactory.XmlTextLiteralToken(attributeValue, null, null));
            return attribute.WithValue(attribute.ValueNode.WithTextTokens(textTokens));
        }

        public static XmlAttributeSyntax WithPrefixName(this XmlAttributeSyntax attribute, string prefixName)
        {
            var existingName = attribute.NameNode;
            var existingPrefix = existingName.PrefixNode;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static XmlElementSyntax AddChild<TSelf>(this TSelf parent, XmlElementBaseSyntax child)
            where TSelf : XmlElementBaseSyntax
        {
            return parent.WithContent(parent.Content.Add(child));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static XmlElementSyntax AddChild<TSelf, TChild>(this TSelf parent, TChild child, out int index)
            where TSelf : XmlElementBaseSyntax
            where TChild : XmlElementBaseSyntax
        {
            return parent.WithContent(parent.Content.Add(child, out index));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static XmlElementSyntax InsertChild<TSelf>(this TSelf parent, XmlElementBaseSyntax child, int index)
            where TSelf : XmlElementBaseSyntax
        {
            return index == -1 ? AddChild(parent, child) : parent.WithContent(parent.Content.Insert(index, child));
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

        public static T SetAttribute<T>(this T element, string attributeName, string value)
            where T : XmlElementBaseSyntax
        {
            XmlAttributeSyntax attribute = element.GetAttribute(attributeName);
            XmlStringSyntax newValue = SyntaxFactory.XmlString(
                SyntaxFactory.Punctuation(SyntaxKind.DoubleQuoteToken, "\"", null, null),
                SyntaxFactory.List([
                    SyntaxFactory.XmlTextLiteralToken(value, null, null)
                ]),
                SyntaxFactory.Punctuation(SyntaxKind.DoubleQuoteToken, "\"", null, null)
            );

            if (attribute is not null)
            {
                return element.ReplaceNode(
                    attribute,
                    attribute.WithValue(newValue)
                );
            }

            var spaceBeginning = element.AttributesNode.Count > 0
                ? !element.AttributesNode.Last().HasTrailingTrivia
                : !element.NameNode.HasTrailingTrivia;

            if (element is XmlEmptyElementSyntax { NameNode.HasTrailingTrivia: true })
            {
                newValue = newValue.WithTrailingTrivia(SyntaxFactory.Space);
            }

            T newRule = element.AddAttribute(
                SyntaxFactory.XmlAttribute(
                    SyntaxFactory.XmlName(null, SyntaxFactory.XmlNameToken(attributeName, spaceBeginning ? SyntaxFactory.Space : null, null)),
                    SyntaxFactory.Punctuation(SyntaxKind.EqualsToken, "=", null, null),
                    newValue
                )
            );

            return newRule;
        }

        public static XmlElementSyntax GetOrAddElement(this XmlElementBaseSyntax root, string name, out XmlElementBaseSyntax result, Func<XmlElementBaseSyntax, XmlElementBaseSyntax> configure = null)
        {
            if (name.Contains("/"))
            {
                var parts = name.Split('/');
                return GetOrAddByPath(parts, root, out result, configure);
            }

            return root.GetOrAddElementCore(name, out result, configure).Node;
        }

        public static XmlElementSyntax AddElement(this XmlElementBaseSyntax root, string name, out XmlElementBaseSyntax result, Func<XmlElementBaseSyntax, XmlElementBaseSyntax> configure = null)
        {
            if (name.Contains("/"))
            {
                var parts = name.Split('/');
                var parentPath = new ArraySegment<string>(parts, 0, parts.Length - 1);
                XmlElementSyntax newRoot = GetOrAddByPath(parentPath, root, out XmlElementBaseSyntax parent, configure);
                XmlElementSyntax newParent = parent.AddElementCore(parts[parts.Length - 1], out result, configure).Node;

                return newRoot.ReplaceNode(parent, newParent);
            }

            return root.AddElementCore(name, out result, configure).Node;
        }

        private static XmlElementSyntax GetOrAddByPath<T>(T parts, XmlElementBaseSyntax root, out XmlElementBaseSyntax result, Func<XmlElementBaseSyntax, XmlElementBaseSyntax> configure)
            where T : IList<string>
        {
            var i = 0;

            var path = new List<int>();
            XmlElementSyntax parent = root.GetOrAddElementCore(parts[i], out result, configure).Node;

            while (++i < parts.Count)
            {
                var (next, changed, index) = result.GetOrAddElementCore(parts[i], out XmlElementBaseSyntax nextResult, configure);

                if (changed)
                {
                    path.Clear();

                    if (!parent.TryReplaceXmlNode(result, next, out parent, path))
                    {
                        throw new InvalidOperationException();
                    }

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

        private static (XmlElementSyntax Node, bool Changed, int Index) GetOrAddElementCore(this XmlElementBaseSyntax root, string name, out XmlElementBaseSyntax result, Func<XmlElementBaseSyntax, XmlElementBaseSyntax> configure)
        {
            SyntaxList<SyntaxNode>.Enumerator enumerator = root.Content.GetEnumerator();

            while (enumerator.MoveNext())
            {
                if (enumerator.Current is not XmlElementBaseSyntax child)
                {
                    continue;
                }

                if (name.Equals(child.Name, StringComparison.Ordinal))
                {
                    result = (XmlElementSyntax) child;
                    return ((XmlElementSyntax)root, false, enumerator.CurrentIndex);
                }
            }

            var (newRoot, index) = root.AddElementCore(name, out result, configure);

            return (newRoot, true, index);
        }

        private static (XmlElementSyntax Node, int Index) AddElementCore(this XmlElementBaseSyntax root, string name, out XmlElementBaseSyntax result, Func<XmlElementBaseSyntax, XmlElementBaseSyntax> configure)
        {
            result = SyntaxFactory.XmlEmptyElement(
                SyntaxFactory.LessThan,
                SyntaxFactory.XmlName(null, SyntaxFactory.XmlNameToken(name, null, SyntaxFactory.Space)),
                default(SyntaxNode),
                SyntaxFactory.SlashGreaterThan
            );

            if (configure is not null)
            {
                result = configure(result);
            }

            AddLeadingTrivia(root, ref result);
            XmlElementSyntax newRoot = root.AddChild(result, out var index);
            EnsureRootTrivia(ref newRoot);

            result = (XmlElementBaseSyntax) newRoot.Content[index];
            return (newRoot, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TSelf NormalizeTrivia<TSelf>(this TSelf node, XmlElementBaseSyntax parent, int extra = 1)
            where TSelf : XmlElementBaseSyntax
        {
            if (parent != null)
            {
                AddLeadingTrivia(parent, ref node, extra);
            }

            if (node is XmlElementSyntax element)
            {
                EnsureRootTrivia(ref element);
                node = (TSelf)(object)element;
            }

            if (node is XmlElementSyntax { Content: { Count: > 0 } content })
            {
                var newContent = new SyntaxListBuilder<SyntaxNode>(content.Count);

                foreach (SyntaxNode child in content)
                {
                    SyntaxNode newChild = child;

                    if (newChild is XmlElementBaseSyntax childElement)
                    {
                        newChild = NormalizeTrivia(childElement, parent, extra + 1);
                    }

                    newContent.Add(newChild);
                }

                node = (TSelf)(object)node.WithContent(newContent.ToList());
            }

            return node;
        }

        private static void EnsureRootTrivia(ref XmlElementSyntax newRoot)
        {
            if (!newRoot.EndTag.HasLeadingTrivia && newRoot.StartTag.HasLeadingTrivia)
            {
                newRoot = newRoot.WithEndTag(newRoot.EndTag.WithLeadingTrivia(newRoot.StartTag.GetLeadingTrivia()));
            }
        }

        private static void AddLeadingTrivia<TSelf>(XmlElementBaseSyntax root, ref TSelf result, int extra = 1)
            where TSelf : XmlElementBaseSyntax
        {
            if (root.HasLeadingTrivia)
            {
                result = result.WithLeadingTrivia(CalculateNewTrivia(root, root.GetLeadingTrivia(), extra));
            }
        }

        internal static SyntaxTriviaList CalculateNewTrivia(XmlElementBaseSyntax root, SyntaxTriviaList trivia, int extra = 1)
        {
            var depth = GetDepth(root);
            var last = trivia.Last().Text;

            if (depth > 0)
            {
                var additionalLength = last.Length / depth;
                var substr = last.Substring(0, additionalLength);

                for (var i = 0; i < extra; i++)
                {
                    last += substr;
                }
            }
            else
            {
                last += last;
            }

            trivia = trivia.Replace(trivia.Last(), SyntaxFactory.WhitespaceTrivia(last));
            return trivia;
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
