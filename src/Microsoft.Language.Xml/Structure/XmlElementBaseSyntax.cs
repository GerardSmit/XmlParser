using System;
using System.Collections.Generic;
using Microsoft.Language.Xml.Collections;

namespace Microsoft.Language.Xml
{
    public abstract class XmlElementBaseSyntax : XmlNodeSyntax, INamedXmlNode
    {
        internal XmlElementBaseSyntax(Green green, SyntaxNode parent, int position) : base(green, parent, position)
        {
        }

        public abstract string Name { get; }
        public abstract string Value { get; }
        public abstract XmlNameSyntax NameNode { get; }
        public abstract SyntaxList<SyntaxNode> Content { get; }
        public XmlElementBaseSyntax AsElement => this;

        public new XmlElementBaseSyntax Parent => (XmlElementBaseSyntax)base.Parent;

        public abstract XmlElementEnumerator Elements { get; }

        public abstract SyntaxList<XmlAttributeSyntax> AttributesNode { get; }

        public IEnumerable<XmlAttributeSyntax> Attributes => AttributesNode;

        public abstract XmlElementSyntax WithContent(SyntaxList<SyntaxNode> newContent);

        protected internal abstract XmlElementBaseSyntax WithAttributes(SyntaxList<XmlAttributeSyntax> newAttributes);

        protected internal abstract XmlElementBaseSyntax WithName(XmlNameSyntax newName);

        public XmlAttributeSyntax GetAttribute(string localName, string prefix = null)
        {
            foreach (XmlAttributeSyntax attr in AttributesNode)
            {
                if (string.Equals(attr.NameNode.LocalName, localName, StringComparison.Ordinal) &&
                    string.Equals(attr.NameNode.Prefix, prefix, StringComparison.Ordinal))
                {
                    return attr;
                }
            }

            return null;
        }

        public string GetAttributeValue(string localName, string prefix = null)
        {
            return GetAttribute(localName, prefix)?.Value;
        }

        public string this[string attributeName] => GetAttributeValue(attributeName);
    }
}
