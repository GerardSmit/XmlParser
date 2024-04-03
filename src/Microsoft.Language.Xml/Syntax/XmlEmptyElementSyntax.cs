using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Language.Xml.Collections;

namespace Microsoft.Language.Xml
{
    using InternalSyntax;

    public class XmlEmptyElementSyntax : XmlElementBaseSyntax, INamedXmlNode
    {
        internal new class Green : XmlNodeSyntax.Green
        {
            readonly PunctuationSyntax.Green lessThanToken;
            readonly XmlNameSyntax.Green name;
            readonly GreenNode attributes;
            readonly PunctuationSyntax.Green slashGreaterThanToken;

            internal PunctuationSyntax.Green LessThanToken => lessThanToken;
            internal XmlNameSyntax.Green NameNode => name;
            internal GreenNode AttributesNode => attributes;
            internal PunctuationSyntax.Green SlashGreaterThanToken => slashGreaterThanToken;

            internal Green(PunctuationSyntax.Green lessThanToken, XmlNameSyntax.Green name, GreenNode attributes, PunctuationSyntax.Green slashGreaterThanToken)
                : base(SyntaxKind.XmlEmptyElement)
            {
                this.SlotCount = 4;
                this.lessThanToken = lessThanToken;
                AdjustWidth(lessThanToken);
                this.name = name;
                AdjustWidth(name);
                this.attributes = attributes;
                AdjustWidth(attributes);
                this.slashGreaterThanToken = slashGreaterThanToken;
                AdjustWidth(slashGreaterThanToken);
            }

            internal Green(PunctuationSyntax.Green lessThanToken, XmlNameSyntax.Green name, GreenNode attributes, PunctuationSyntax.Green slashGreaterThanToken, DiagnosticInfo[] diagnostics, SyntaxAnnotation[] annotations)
                : base(SyntaxKind.XmlEmptyElement, diagnostics, annotations)
            {
                this.SlotCount = 4;
                this.lessThanToken = lessThanToken;
                AdjustWidth(lessThanToken);
                this.name = name;
                AdjustWidth(name);
                this.attributes = attributes;
                AdjustWidth(attributes);
                this.slashGreaterThanToken = slashGreaterThanToken;
                AdjustWidth(slashGreaterThanToken);
            }

            internal override SyntaxNode CreateRed(SyntaxNode parent, int position) => new XmlEmptyElementSyntax(this, parent, position);

            internal override GreenNode GetSlot(int index)
            {
                switch (index)
                {
                    case 0: return lessThanToken;
                    case 1: return name;
                    case 2: return attributes;
                    case 3: return slashGreaterThanToken;
                }
                throw new InvalidOperationException();
            }

            internal override GreenNode Accept(InternalSyntax.SyntaxVisitor visitor)
            {
                return visitor.VisitXmlEmptyElement(this);
            }

            internal override GreenNode SetDiagnostics(DiagnosticInfo[] diagnostics)
            {
                return new Green(lessThanToken, name, attributes, slashGreaterThanToken, diagnostics, GetAnnotations());
            }

            internal override GreenNode SetAnnotations(SyntaxAnnotation[] annotations)
            {
                return new Green(lessThanToken, name, attributes, slashGreaterThanToken, GetDiagnostics(), annotations);
            }
        }

        PunctuationSyntax lessThanToken;
        XmlNameSyntax nameNode;
        SyntaxNode attributesNode;
        PunctuationSyntax slashGreaterThanToken;

        public PunctuationSyntax LessThanToken => GetRed(ref lessThanToken, 0);
        public override XmlNameSyntax NameNode => GetRed(ref nameNode, 1);
        public override SyntaxList<XmlAttributeSyntax> AttributesNode => new(GetRed(ref attributesNode, 2));

        public PunctuationSyntax SlashGreaterThanToken => GetRed(ref slashGreaterThanToken, 3);

        internal XmlEmptyElementSyntax(Green green, SyntaxNode parent, int position)
            : base(green, parent, position)
        {

        }

        public override SyntaxNode Accept(SyntaxVisitor visitor)
        {
            return visitor.VisitXmlEmptyElement(this);
        }

        internal override SyntaxNode GetCachedSlot(int index)
        {
            switch (index)
            {
                case 0: return lessThanToken;
                case 1: return nameNode;
                case 2: return attributesNode;
                case 3: return slashGreaterThanToken;
                default: return null;
            }
        }

        internal override SyntaxNode GetNodeSlot(int slot)
        {
            switch (slot)
            {
                case 0: return LessThanToken;
                case 1: return NameNode;
                case 2: return GetRed(ref attributesNode, 2);
                case 3: return SlashGreaterThanToken;
                default: return null;
            }
        }

        public override string Name => NameNode?.FullName;

        public override SyntaxList<SyntaxNode> Content => default(SyntaxList<SyntaxNode>);

        public override string Value => "";

        public XmlElementEnumerator XmlElements => default;

        public override XmlElementEnumerator Elements => default;

        public XmlEmptyElementSyntax Update(PunctuationSyntax lessThanToken, XmlNameSyntax name, SyntaxList<XmlAttributeSyntax> attributes, PunctuationSyntax slashGreaterThanToken)
        {
            if (lessThanToken != this.LessThanToken || name != this.NameNode || attributes != this.AttributesNode || slashGreaterThanToken != this.SlashGreaterThanToken)
            {
                var newNode = SyntaxFactory.XmlEmptyElement(lessThanToken, name, attributes, slashGreaterThanToken);
                var annotations = this.GetAnnotations();
                if (annotations != null && annotations.Length > 0)
                    return newNode.WithAnnotations(annotations);
                return newNode;
            }

            return this;
        }

        public XmlEmptyElementSyntax WithLessThanToken(PunctuationSyntax lessThanToken)
        {
            return this.Update(lessThanToken, this.NameNode, this.AttributesNode, this.SlashGreaterThanToken);
        }

        protected internal override XmlElementBaseSyntax WithName(XmlNameSyntax name)
        {
            return this.Update(this.LessThanToken, name, this.AttributesNode, this.SlashGreaterThanToken);
        }

        protected internal override XmlElementBaseSyntax WithAttributes(SyntaxList<XmlAttributeSyntax> attributes)
        {
            return this.Update(this.LessThanToken, this.NameNode, attributes, this.SlashGreaterThanToken);
        }

        // This method has to convert to an XmlElementSyntax
        public override XmlElementSyntax WithContent(SyntaxList<SyntaxNode> content)
        {
            var greaterThanToken = SyntaxFactory.Punctuation(SyntaxKind.GreaterThanToken, ">", null, null);
            var startName = this.AttributesNode.Count == 0 ? this.NameNode.WithTrailingTrivia() : this.NameNode.WithTrailingTrivia(SyntaxFactory.Space);
            var startTag = SyntaxFactory.XmlElementStartTag(this.LessThanToken, startName, this.AttributesNode, greaterThanToken);
            var lessThanSlashToken = SyntaxFactory.Punctuation(SyntaxKind.LessThanSlashToken, "</", null, null);
            var endTag = SyntaxFactory.XmlElementEndTag(lessThanSlashToken, this.NameNode.WithTrailingTrivia(), greaterThanToken);
            var newNode = SyntaxFactory.XmlElement(startTag, content, endTag);
            var annotations = this.GetAnnotations();
            if (annotations != null && annotations.Length > 0)
                return newNode.WithAnnotations(annotations);
            return newNode;
        }

        public XmlEmptyElementSyntax WithSlashGreaterThanToken(PunctuationSyntax slashGreaterThanToken)
        {
            return this.Update(this.LessThanToken, this.NameNode, this.AttributesNode, slashGreaterThanToken);
        }

        public XmlElementBaseSyntax AddAttributes(params XmlAttributeSyntax[] items)
        {
            return this.WithAttributes(this.AttributesNode.AddRange(items));
        }
    }
}
