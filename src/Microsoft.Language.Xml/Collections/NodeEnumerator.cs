using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Language.Xml.InternalSyntax;

namespace Microsoft.Language.Xml.Collections
{
    public struct SyntaxNodeEnumerator(
        SyntaxNode node
    ) : IEnumerable<SyntaxNode>, IEnumerator<SyntaxNode>
    {
        private int _index;

        public int CurrentIndex => _index - 1;

        public SyntaxNodeEnumerator GetEnumerator()
        {
            return this;
        }

        IEnumerator<SyntaxNode> IEnumerable<SyntaxNode>.GetEnumerator()
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
            while (_index < node.SlotCount)
            {
                SyntaxNode current = node.GetNodeSlot(_index);
                _index++;

                if (current != null)
                {
                    Current = current;
                    return true;
                }
            }

            Current = null;
            return false;
        }

        public void Reset()
        {
            _index = 0;
        }

        public SyntaxNode Current { get; private set; }

        object IEnumerator.Current => Current;
    }
}
