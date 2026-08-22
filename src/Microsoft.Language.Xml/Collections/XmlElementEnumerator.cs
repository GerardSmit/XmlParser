using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Language.Xml.Collections
{
    public struct XmlElementEnumerator(
        SyntaxList<SyntaxNode> content
    ) : IEnumerable<XmlElementBaseSyntax>, IEnumerator<XmlElementBaseSyntax>
    {
        private int _current;

        public int CurrentIndexInContent => _current - 1;

        /// <summary>
        /// A walk over the whole sequence. An enumerator that has already been advanced hands out
        /// the sequence from the start rather than the remainder of its own, so that enumerating
        /// twice yields the same elements twice.
        /// </summary>
        public XmlElementEnumerator GetEnumerator()
        {
            return new XmlElementEnumerator(content);
        }

        IEnumerator<XmlElementBaseSyntax> IEnumerable<XmlElementBaseSyntax>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        void IDisposable.Dispose()
        {
        }

        /// <summary>
        /// The first element. Throws when there is none.
        /// </summary>
        public XmlElementBaseSyntax First()
        {
            return FirstOrDefault() ?? throw new InvalidOperationException("Sequence contains no elements.");
        }

        /// <summary>
        /// The first element, or <c>null</c> when there is none.
        /// </summary>
        public XmlElementBaseSyntax? FirstOrDefault()
        {
            // A fresh enumerator, not a copy of this one: "first" has to mean the first whether or
            // not this enumerator has already been walked, and copying the position makes it mean
            // "next" instead. GetEnumerator leaves the caller's own position untouched either way.
            XmlElementEnumerator enumerator = GetEnumerator();

            return enumerator.MoveNext() ? enumerator.Current : null;
        }

        public bool MoveNext()
        {
            switch (content.Node)
            {
                case SyntaxList list:
                    while (_current < list.GreenNode.SlotCount)
                    {
                        if (list.GetNodeSlot(_current) is XmlElementBaseSyntax element)
                        {
                            Current = element;
                            _current++;
                            return true;
                        }

                        _current++;
                    }

                    return false;
                case XmlElementBaseSyntax elementSyntax when _current == 0:
                    Current = elementSyntax;
                    _current++;
                    return true;
                default:
                    return false;
            }
        }

        public void Reset()
        {
            _current = 0;
        }

        public XmlElementBaseSyntax Current { get; private set; } = null!;

        object IEnumerator.Current => Current;
    }
}
