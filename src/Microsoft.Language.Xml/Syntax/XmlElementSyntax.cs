using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Language.Xml.Collections;

namespace Microsoft.Language.Xml
{
    using InternalSyntax;

    public class XmlElementSyntax : XmlElementBaseSyntax
    {
        internal new class Green : XmlNodeSyntax.Green
        {
            readonly XmlElementStartTagSyntax.Green startTag;
            readonly GreenNode content;
            readonly XmlElementEndTagSyntax.Green endTag;

            internal XmlElementStartTagSyntax.Green StartTag => startTag;
            internal GreenNode Content => content;
            internal XmlElementEndTagSyntax.Green EndTag => endTag;

            internal Green(XmlElementStartTagSyntax.Green startTag, GreenNode content, XmlElementEndTagSyntax.Green endTag)
                : base(SyntaxKind.XmlElement)
            {
                this.SlotCount = 3;
                this.startTag = startTag;
                AdjustWidth(startTag);
                this.content = content;
                AdjustWidth(content);
                this.endTag = endTag;
                AdjustWidth(endTag);
            }

            internal Green(XmlElementStartTagSyntax.Green startTag, GreenNode content, XmlElementEndTagSyntax.Green endTag, DiagnosticInfo[] diagnostics, SyntaxAnnotation[] annotations)
                : base(SyntaxKind.XmlElement, diagnostics, annotations)
            {
                this.SlotCount = 3;
                this.startTag = startTag;
                AdjustWidth(startTag);
                this.content = content;
                AdjustWidth(content);
                this.endTag = endTag;
                AdjustWidth(endTag);
            }

            internal override SyntaxNode CreateRed(SyntaxNode parent, int position) => new XmlElementSyntax(this, parent, position);

            internal override GreenNode GetSlot(int index)
            {
                switch (index)
                {
                    case 0: return startTag;
                    case 1: return content;
                    case 2: return endTag;
                }
                throw new InvalidOperationException();
            }

            internal override GreenNode Accept(InternalSyntax.SyntaxVisitor visitor)
            {
                return visitor.VisitXmlElement(this);
            }

            internal override GreenNode SetDiagnostics(DiagnosticInfo[] diagnostics)
            {
                return new Green(startTag, content, endTag, diagnostics, GetAnnotations());
            }

            internal override GreenNode SetAnnotations(SyntaxAnnotation[] annotations)
            {
                return new Green(startTag, content, endTag, GetDiagnostics(), annotations);
            }
        }

        internal new Green GreenNode => (Green)base.GreenNode;

        XmlElementStartTagSyntax startTag;
        SyntaxNode content;
        XmlElementEndTagSyntax endTag;

        public XmlElementStartTagSyntax StartTag => GetRed(ref startTag, 0);
        public override SyntaxList<SyntaxNode> Content => new SyntaxList<SyntaxNode>(GetRed(ref content, 1));
        public XmlElementEndTagSyntax EndTag => GetRed(ref endTag, 2);

        internal XmlElementSyntax(Green green, SyntaxNode parent, int position)
            : base(green, parent, position)
        {

        }

        public override SyntaxNode Accept(SyntaxVisitor visitor)
        {
            return visitor.VisitXmlElement(this);
        }

        internal override SyntaxNode GetCachedSlot(int index)
        {
            switch (index)
            {
                case 0: return startTag;
                case 1: return content;
                case 2: return endTag;
                default: return null;
            }
        }

        internal override SyntaxNode GetNodeSlot(int slot)
        {
            switch (slot)
            {
                case 0: return StartTag;
                case 1: return GetRed(ref content, 1);
                case 2: return EndTag;
                default: return null;
            }
        }

        public override XmlNameSyntax NameNode => StartTag?.NameNode;

        public override string Name => StartTag?.Name;

        public XmlElementEnumerator XmlElements => new(Content);

        public override string Value => Content.ToFullString();

        public override XmlElementEnumerator Elements => new(Content);

        public override SyntaxList<XmlAttributeSyntax> AttributesNode => StartTag?.AttributesNode ?? default;

        public XmlElementSyntax Update(XmlElementStartTagSyntax startTag, SyntaxList<SyntaxNode> content, XmlElementEndTagSyntax endTag)
        {
            if (startTag != this.StartTag || content != this.Content || endTag != this.EndTag)
            {
                var newNode = SyntaxFactory.XmlElement(startTag, content, endTag);
                var annotations = this.GetAnnotations();
                if (annotations != null && annotations.Length > 0)
                    return newNode.WithAnnotations(annotations);
                return newNode;
            }

            return this;
        }

        public XmlElementSyntax WithStartTag(XmlElementStartTagSyntax startTag)
        {
            return this.Update(startTag, this.Content, this.EndTag);
        }

        public override XmlElementSyntax WithContent(SyntaxList<SyntaxNode> content)
        {
            return this.Update(this.StartTag, content, this.EndTag);
        }

        protected internal override XmlElementBaseSyntax WithAttributes(SyntaxList<XmlAttributeSyntax> newAttributes)
        {
            return this.WithStartTag(this.StartTag.WithAttributes(newAttributes));
        }

        protected internal override XmlElementBaseSyntax WithName(XmlNameSyntax newName)
        {
            return this.WithStartTag(this.StartTag.WithName(newName));
        }

        public XmlElementSyntax WithEndTag(XmlElementEndTagSyntax endTag)
        {
            return this.Update(this.StartTag, this.Content, endTag);
        }

        public XmlElementSyntax AddStartTagAttributes(params XmlAttributeSyntax[] items)
        {
            return this.WithStartTag(this.StartTag.WithAttributes(this.StartTag.AttributesNode.AddRange(items)));
        }

        public XmlElementSyntax AddContent(params XmlNodeSyntax[] items)
        {
            return this.WithContent(this.Content.AddRange(items));
        }
    }
}
