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

        public XmlElementBaseSyntax Current { get; private set; }

        object IEnumerator.Current => Current;
    }
}
