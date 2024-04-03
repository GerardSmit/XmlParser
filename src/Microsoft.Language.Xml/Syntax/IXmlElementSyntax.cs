using System.Collections.Generic;
using Microsoft.Language.Xml.Collections;

namespace Microsoft.Language.Xml
{
    public interface IXmlElementSyntax
    {
        string Name { get; }
        XmlNameSyntax NameNode { get; }
        SyntaxList<SyntaxNode> Content { get; }
        XmlElementBaseSyntax Parent { get; }
        XmlElementEnumerator XmlElements { get; }
        IEnumerable<XmlAttributeSyntax> Attributes { get; }
        SyntaxList<XmlAttributeSyntax> AttributesNode { get; }
        XmlAttributeSyntax GetAttribute(string localName, string prefix = null);
        string GetAttributeValue(string localName, string prefix = null);
        XmlElementBaseSyntax AsElement { get; }
        XmlNodeSyntax AsNode { get; }
        string ToFullString();
        XmlElementSyntax WithContent(SyntaxList<SyntaxNode> newContent);
        XmlElementBaseSyntax WithName(XmlNameSyntax newName);
        XmlElementBaseSyntax WithAttributes(IEnumerable<XmlAttributeSyntax> newAttributes);
        XmlElementBaseSyntax WithAttributes(SyntaxList<XmlAttributeSyntax> newAttributes);
    }

    public interface IXmlElementSyntax<out TSelf> : IXmlElementSyntax
        where TSelf : IXmlElementSyntax<TSelf>
    {
        new TSelf WithName(XmlNameSyntax newName);
        new TSelf WithAttributes(IEnumerable<XmlAttributeSyntax> newAttributes);
        new TSelf WithAttributes(SyntaxList<XmlAttributeSyntax> newAttributes);
    }
}
