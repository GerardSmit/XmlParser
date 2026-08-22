using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Language.Xml.Collections;



namespace Microsoft.Language.Xml
{
    using InternalSyntax;

    public class XmlEmptyElementSyntax : XmlElementBaseSyntax, INamedXmlNode
    {
        internal new class Green : XmlNodeSyntax.Green
        {
            readonly PunctuationSyntax.Green? lessThanToken;
            readonly XmlNameSyntax.Green? name;
            readonly GreenNode? attributes;
            readonly PunctuationSyntax.Green? slashGreaterThanToken;

            internal PunctuationSyntax.Green? LessThanToken => lessThanToken;
            internal XmlNameSyntax.Green? NameNode => name;
            internal GreenNode? AttributesNode => attributes;
            internal PunctuationSyntax.Green? SlashGreaterThanToken => slashGreaterThanToken;

            internal Green(PunctuationSyntax.Green? lessThanToken, XmlNameSyntax.Green? name, GreenNode? attributes, PunctuationSyntax.Green? slashGreaterThanToken)
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

            internal Green(PunctuationSyntax.Green? lessThanToken, XmlNameSyntax.Green? name, GreenNode? attributes, PunctuationSyntax.Green? slashGreaterThanToken, DiagnosticInfo[]? diagnostics, SyntaxAnnotation[] annotations)
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

            internal override SyntaxNode CreateRed(SyntaxNode? parent, int position) => new XmlEmptyElementSyntax(this, parent, position);

            internal override GreenNode? GetSlot(int index)
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

            internal override GreenNode SetDiagnostics(DiagnosticInfo[]? diagnostics)
            {
                return new Green(lessThanToken, name, attributes, slashGreaterThanToken, diagnostics, GetAnnotations());
            }

            internal override GreenNode SetAnnotations(SyntaxAnnotation[] annotations)
            {
                return new Green(lessThanToken, name, attributes, slashGreaterThanToken, GetDiagnostics(), annotations);
            }
        }

        PunctuationSyntax? lessThanToken;
        XmlNameSyntax? nameNode;
        SyntaxNode? attributesNode;
        PunctuationSyntax? slashGreaterThanToken;

        public PunctuationSyntax LessThanToken => GetRed(ref lessThanToken, 0)!;
        public override XmlNameSyntax NameNode => GetRed(ref nameNode, 1)!;
        public override SyntaxList<XmlAttributeSyntax> AttributesNode => new(GetRed(ref attributesNode, 2));
        public PunctuationSyntax SlashGreaterThanToken => GetRed(ref slashGreaterThanToken, 3)!;

        internal XmlEmptyElementSyntax(Green green, SyntaxNode? parent, int position)
            : base(green, parent, position)
        {

        }

        public override SyntaxNode Accept(SyntaxVisitor visitor)
        {
            return visitor.VisitXmlEmptyElement(this);
        }

        internal override SyntaxNode? GetCachedSlot(int index)
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

        internal override SyntaxNode? GetNodeSlot(int slot)
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

        public override string Name => NameNode.FullName;

        public override SyntaxList<SyntaxNode> Content => default(SyntaxList<SyntaxNode>);

        public override string RawValue => "";

        /// <summary>
        /// Empty, positioned where content would start if the element were opened.
        /// </summary>
        public override TextSpan ContentSpan => new TextSpan(SlashGreaterThanToken.Span.Start, 0);

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
            SyntaxList<XmlAttributeSyntax> attributes = this.AttributesNode;
            XmlNameSyntax startName;

            if (attributes.Count == 0)
            {
                // Nothing left to separate the name from: the ">" goes straight after it.
                startName = this.NameNode.WithTrailingTrivia();
            }
            else
            {
                // Whatever already separates the name from the first attribute is kept as it is;
                // a space is only added when neither side carries one, which would otherwise run
                // them together.
                startName = this.NameNode.HasTrailingTrivia || attributes[0].HasLeadingTrivia
                    ? this.NameNode
                    : this.NameNode.WithTrailingTrivia(SyntaxFactory.Space);

                // Trailing whitespace on the last attribute only ever separated it from the "/>".
                // The ">" replacing it needs no separator, and keeping the space would show up as
                // a diff on a line the caller never edited. Only the attribute's own trivia is
                // touched: a line break laying the attributes out over several lines belongs to
                // the "/>" token instead, and goes when that token does - there is nothing this
                // method can do to keep it.
                var lastIndex = attributes.Count - 1;
                XmlAttributeSyntax last = attributes[lastIndex];

                if (last.HasTrailingTrivia && IsWhitespaceOnly(last.GetTrailingTrivia()))
                {
                    attributes = attributes.Replace(lastIndex, last.WithTrailingTrivia());
                }
            }

            var startTag = SyntaxFactory.XmlElementStartTag(this.LessThanToken, startName, attributes, greaterThanToken);
            var lessThanSlashToken = SyntaxFactory.Punctuation(SyntaxKind.LessThanSlashToken, "</", null, null);
            var endTag = SyntaxFactory.XmlElementEndTag(lessThanSlashToken, this.NameNode.WithTrailingTrivia(), greaterThanToken);
            var newNode = SyntaxFactory.XmlElement(startTag, content, endTag);
            var annotations = this.GetAnnotations();
            if (annotations != null && annotations.Length > 0)
                return newNode.WithAnnotations(annotations);

            return newNode;
        }

        private static bool IsWhitespaceOnly(SyntaxTriviaList trivia)
        {
            foreach (SyntaxTrivia item in trivia)
            {
                if (item.Kind != SyntaxKind.WhitespaceTrivia)
                {
                    return false;
                }
            }

            return true;
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
