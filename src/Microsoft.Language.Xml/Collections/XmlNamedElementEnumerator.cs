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
        private readonly bool _matchAnyPrefix;
        private readonly StringComparison _comparison;
        private XmlElementEnumerator _elements;

        public XmlNamedElementEnumerator(SyntaxList<SyntaxNode> content, string localName, string? prefix)
            : this(content, localName, prefix, matchAnyPrefix: false, StringComparison.Ordinal)
        {
        }

        public XmlNamedElementEnumerator(
            SyntaxList<SyntaxNode> content,
            string localName,
            string? prefix,
            bool matchAnyPrefix,
            StringComparison comparison)
        {
            _localName = localName;
            _prefix = prefix;
            _matchAnyPrefix = matchAnyPrefix;
            _comparison = comparison;
            _elements = new XmlElementEnumerator(content);
            Current = null!;
        }

        /// <summary>
        /// A walk over the whole sequence. An enumerator that has already been advanced hands out
        /// the sequence from the start rather than the remainder of its own, so that enumerating
        /// twice yields the same elements twice.
        /// </summary>
        public XmlNamedElementEnumerator GetEnumerator()
        {
            XmlNamedElementEnumerator enumerator = this;
            enumerator.Reset();

            return enumerator;
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
            // A fresh enumerator, not a copy of this one: "first" has to mean the first whether or
            // not this enumerator has already been walked, and copying the position makes it mean
            // "next" instead. GetEnumerator leaves the caller's own position untouched either way.
            XmlNamedElementEnumerator enumerator = GetEnumerator();

            return enumerator.MoveNext() ? enumerator.Current : null;
        }

        public bool MoveNext()
        {
            while (_elements.MoveNext())
            {
                XmlElementBaseSyntax element = _elements.Current;

                if (XmlNameMatcher.Matches(element.NameNode, _localName, _prefix, _matchAnyPrefix, _comparison))
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
            Current = null!;
        }

        public XmlElementBaseSyntax Current { get; private set; }

        object IEnumerator.Current => Current;
    }
}
