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

        /// <summary>
        /// A walk over the whole sequence. An enumerator that has already been advanced hands out
        /// the sequence from the start rather than the remainder of its own, so that enumerating
        /// twice yields the same nodes twice.
        /// </summary>
        public SyntaxNodeEnumerator GetEnumerator()
        {
            return new SyntaxNodeEnumerator(node);
        }

        IEnumerator<SyntaxNode> IEnumerable<SyntaxNode>.GetEnumerator()
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
            // A fresh enumerator, not a copy of this one: "first" has to mean the first whether or
            // not this enumerator has already been walked, and copying the position makes it mean
            // "next" instead. GetEnumerator leaves the caller's own position untouched either way.
            SyntaxNodeEnumerator enumerator = GetEnumerator();

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
