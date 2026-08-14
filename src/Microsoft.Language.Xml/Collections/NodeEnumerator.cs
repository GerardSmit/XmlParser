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

        /// <summary>
        /// The first node. Throws when there is none.
        /// </summary>
        public SyntaxNode First()
        {
            return FirstOrDefault() ?? throw new InvalidOperationException("Sequence contains no elements.");
        }

        /// <summary>
        /// The first node, or <c>null</c> when there is none.
        /// </summary>
        public SyntaxNode? FirstOrDefault()
        {
            // Enumerate a copy so the caller's position is untouched.
            SyntaxNodeEnumerator enumerator = this;

            return enumerator.MoveNext() ? enumerator.Current : null;
        }

        public bool MoveNext()
        {
            while (_index < node.SlotCount)
            {
                SyntaxNode? current = node.GetNodeSlot(_index);
                _index++;

                if (current != null)
                {
                    Current = current;
                    return true;
                }
            }

            Current = null!;
            return false;
        }

        public void Reset()
        {
            _index = 0;
        }

        public SyntaxNode Current { get; private set; } = null!;

        object IEnumerator.Current => Current;
    }
}
