using System;
using System.Collections.Generic;
using Microsoft.Language.Xml.Collections;

namespace Microsoft.Language.Xml
{
    public abstract class XmlElementBaseSyntax : XmlNodeSyntax, IXmlElement
    {
        internal XmlElementBaseSyntax(Green green, SyntaxNode parent, int position) : base(green, parent, position)
        {
        }

        public abstract string Name { get; }
        public abstract string Value { get; }

        IEnumerable<KeyValuePair<string, string>> IXmlElement.Attributes => Attributes;

        public XmlElementBaseSyntax AsElement => this;

        public new XmlElementBaseSyntax Parent => (XmlElementBaseSyntax)base.Parent;

        public abstract XmlElementEnumerator Elements { get; }

        public abstract SyntaxList<XmlAttributeSyntax> AttributesNode { get; }

        protected abstract IXmlElementSyntax AsSyntaxElement { get; }

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

        #region IXmlElement Members

        IXmlElement IXmlElement.Parent => Parent;

        IEnumerable<IXmlElement> IXmlElement.Elements => Elements;

        IXmlElementSyntax IXmlElement.AsSyntaxElement => AsSyntaxElement;

        public XmlAttributeEnumerator Attributes => new(AttributesNode);

        #endregion
    }
}
