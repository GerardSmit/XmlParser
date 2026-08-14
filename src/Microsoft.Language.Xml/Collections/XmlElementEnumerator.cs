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

        public XmlElementEnumerator GetEnumerator()
        {
            return this;
        }

        IEnumerator<XmlElementBaseSyntax> IEnumerable<XmlElementBaseSyntax>.GetEnumerator()
        {
            return this;
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
            // Enumerate a copy so the caller's position is untouched.
            XmlElementEnumerator enumerator = this;

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
