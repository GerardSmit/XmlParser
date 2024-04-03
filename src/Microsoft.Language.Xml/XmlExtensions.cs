using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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
        public static string GetContentValue(this IXmlElementSyntax element)
        {
            if (element.Content.Count == 1 && element.Content.First() is XmlCDataSectionSyntax cdata)
                return cdata.TextTokens.ToFullString();
            return element.AsElement.Value;
        }

        /// <summary>
        /// Return a new <see cref="IXmlElementSyntax"/> instance with
        /// the supplied string prefix.
        /// </summary>
        public static TSelf WithPrefixName<TSelf>(this TSelf element, string prefixName)
            where TSelf : class, IXmlElementSyntax<TSelf>
        {
            var existingName = element.NameNode;
            var existingPrefix = existingName.PrefixNode;
            var newName = SyntaxFactory.XmlNameToken(prefixName, null, null);

            return element.WithName(existingName.WithPrefix(existingPrefix.WithName(newName)));
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
        public static XmlElementSyntax AddChild<TSelf>(this TSelf parent, IXmlElementSyntax child)
            where TSelf : class, IXmlElementSyntax<TSelf>
        {
            return parent.WithContent(parent.Content.Add(child.AsNode));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static XmlElementSyntax InsertChild<TSelf>(this TSelf parent, IXmlElementSyntax child, int index)
            where TSelf : class, IXmlElementSyntax<TSelf>
        {
            return index == -1 ? AddChild(parent, child) : parent.WithContent(parent.Content.Insert(index, child.AsNode));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static XmlElementSyntax RemoveChild<TSelf>(this TSelf parent, IXmlElementSyntax child)
            where TSelf : class, IXmlElementSyntax<TSelf>
        {
            return parent.WithContent(parent.Content.Remove(child.AsNode));
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
            where T : class, IXmlElementSyntax<T>
        {
            return self.WithAttributes(self.AttributesNode.AddRange(attributes));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AddAttributes<T>(this IXmlElementSyntax<T> self, IEnumerable<XmlAttributeSyntax> attributes)
            where T : class, IXmlElementSyntax<T>
        {
            return self.WithAttributes(self.AttributesNode.AddRange(attributes));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AddAttribute<T>(this IXmlElementSyntax<T> self, XmlAttributeSyntax attribute)
            where T : class, IXmlElementSyntax<T>
        {
            return self.WithAttributes(self.AttributesNode.Add(attribute));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T RemoveAttribute<T>(this IXmlElementSyntax<T> self, XmlAttributeSyntax attribute)
            where T : class, IXmlElementSyntax<T>
        {
            return self.WithAttributes(self.AttributesNode.Remove(attribute));
        }

        public static T SetAttribute<T>(this T rule, string attributeName, string value)
            where T : XmlElementBaseSyntax, IXmlElementSyntax<T>
        {
            XmlAttributeSyntax attribute = rule.GetAttribute(attributeName);
            XmlStringSyntax newValue = SyntaxFactory.XmlString(
                SyntaxFactory.Punctuation(SyntaxKind.DoubleQuoteToken, "\"", null, null),
                SyntaxFactory.List([
                    SyntaxFactory.XmlTextLiteralToken(value, null, null)
                ]),
                SyntaxFactory.Punctuation(SyntaxKind.DoubleQuoteToken, "\"", null, null)
            );

            if (attribute is not null)
            {
                return rule.ReplaceNode(
                    attribute,
                    attribute.WithValue(newValue)
                );
            }

            T newRule = rule.AddAttribute(
                SyntaxFactory.XmlAttribute(
                    SyntaxFactory.XmlName(null, SyntaxFactory.XmlNameToken(attributeName, SyntaxFactory.Space, null)),
                    SyntaxFactory.Punctuation(SyntaxKind.EqualsToken, "=", null, null),
                    newValue
                )
            );

            return newRule;

        }
    }
}
