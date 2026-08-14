using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Language.Xml.Collections
{
    /// <summary>
    /// Enumerates the child elements matching a given name. Allocation-free when used directly
    /// rather than through <see cref="IEnumerable{T}"/>.
    /// </summary>
    public struct XmlNamedElementEnumerator : IEnumerable<XmlElementBaseSyntax>, IEnumerator<XmlElementBaseSyntax>
    {
        private readonly string _localName;
        private readonly string? _prefix;
        private XmlElementEnumerator _elements;

        public XmlNamedElementEnumerator(SyntaxList<SyntaxNode> content, string localName, string? prefix)
        {
            _localName = localName;
            _prefix = prefix;
            _elements = new XmlElementEnumerator(content);
            Current = null!;
        }

        public XmlNamedElementEnumerator GetEnumerator()
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
        /// The first matching element. Throws when there is none.
        /// </summary>
        public XmlElementBaseSyntax First()
        {
            return FirstOrDefault() ?? throw new InvalidOperationException("Sequence contains no elements.");
        }

        /// <summary>
        /// The first matching element, or <c>null</c> when there is none.
        /// </summary>
        public XmlElementBaseSyntax? FirstOrDefault()
        {
            // Enumerate a copy so the caller's position is untouched.
            XmlNamedElementEnumerator enumerator = this;

            return enumerator.MoveNext() ? enumerator.Current : null;
        }

        public bool MoveNext()
        {
            while (_elements.MoveNext())
            {
                XmlElementBaseSyntax element = _elements.Current;
                XmlNameSyntax name = element.NameNode;

                if (string.Equals(name.LocalName, _localName, StringComparison.Ordinal) &&
                    string.Equals(name.Prefix, _prefix, StringComparison.Ordinal))
                {
                    Current = element;
                    return true;
                }
            }

            Current = null!;
            return false;
        }

        public void Reset()
        {
            _elements.Reset();
        }

        public XmlElementBaseSyntax Current { get; private set; }

        object IEnumerator.Current => Current;
    }
}
