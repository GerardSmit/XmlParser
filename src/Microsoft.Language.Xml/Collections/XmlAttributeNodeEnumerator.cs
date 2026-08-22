using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Language.Xml.Collections
{
    /// <summary>
    /// Enumerates an element's attributes as nodes, so the name, the value and the spans are all
    /// still reachable. Allocation-free when used directly rather than through
    /// <see cref="IEnumerable{T}"/>.
    /// </summary>
    /// <remarks>
    /// The older <see cref="XmlAttributeEnumerator"/> yields name/value pairs, which loses the
    /// nodes and with them every position in the document.
    /// </remarks>
    public struct XmlAttributeNodeEnumerator(
        SyntaxList<XmlAttributeSyntax> attributes
    ) : IEnumerable<XmlAttributeSyntax>, IEnumerator<XmlAttributeSyntax>
    {
        private int _index;

        /// <summary>
        /// The position of <see cref="Current"/> among the attributes, or -1 before the first
        /// <see cref="MoveNext"/> - there is no current attribute to have a position yet.
        /// </summary>
        public int CurrentIndex => _index - 1;

        public XmlAttributeNodeEnumerator GetEnumerator()
        {
            // A fresh walk, so a partly-advanced enumerator still hands out a whole sequence.
            return new XmlAttributeNodeEnumerator(attributes);
        }

        IEnumerator<XmlAttributeSyntax> IEnumerable<XmlAttributeSyntax>.GetEnumerator()
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
        /// The first attribute. Throws when there is none.
        /// </summary>
        public XmlAttributeSyntax First()
        {
            return FirstOrDefault() ?? throw new InvalidOperationException("Sequence contains no elements.");
        }

        /// <summary>
        /// The first attribute, or <c>null</c> when there is none.
        /// </summary>
        public XmlAttributeSyntax? FirstOrDefault()
        {
            // A fresh enumerator, not a copy of this one: "first" has to mean the first whether or
            // not this enumerator has already been walked, and copying the position makes it mean
            // "next" instead. GetEnumerator leaves the caller's own position untouched either way.
            XmlAttributeNodeEnumerator enumerator = GetEnumerator();

            return enumerator.MoveNext() ? enumerator.Current : null;
        }

        public int Count => attributes.Count;

        public bool MoveNext()
        {
            if (_index >= attributes.Count)
            {
                Current = null!;
                return false;
            }

            Current = attributes[_index];
            _index++;
            return true;
        }

        public void Reset()
        {
            _index = 0;
            Current = null!;
        }

        public XmlAttributeSyntax Current { get; private set; } = null!;

        object IEnumerator.Current => Current;
    }
}
