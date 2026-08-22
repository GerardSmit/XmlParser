using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Language.Xml.Collections
{
    /// <summary>
    /// Depth-first walk of every element below a node, in document order, optionally filtered by
    /// name.
    /// </summary>
    /// <remarks>
    /// Enumerating allocates one array to hold the descent and grows it if the document turns out
    /// to be deeper than expected; nothing is allocated per element visited. That is the whole
    /// point of it - reaching "every PackageReference anywhere" through
    /// <c>DescendantNodes().OfType&lt;&gt;()</c> allocates an iterator, a stack and a boxed
    /// enumerator per level.
    /// <br/>
    /// That array is what a walk is made of, so two enumerators cannot share one. Copying this
    /// struct after enumeration has started - assigning it to a second variable, passing it by
    /// value - hands out a second reader of the same walk, and advancing either moves both.
    /// <see cref="GetEnumerator"/>, <see cref="Reset"/> and <see cref="FirstOrDefault"/> all start
    /// a fresh one, so <c>foreach</c> and the LINQ operators are unaffected; a raw copy taken
    /// mid-walk is the one thing to avoid.
    /// </remarks>
    public struct XmlDescendantElementEnumerator : IEnumerable<XmlElementBaseSyntax>, IEnumerator<XmlElementBaseSyntax>
    {
        private const int InitialDepth = 8;

        /// <summary>
        /// The nodes the walk starts from. Held as content rather than as an element so a document,
        /// whose root element should itself be part of the walk, can be enumerated too.
        /// </summary>
        private readonly SyntaxList<SyntaxNode> _rootContent;

        private readonly string? _localName;
        private readonly string? _prefix;
        private readonly bool _matchAnyPrefix;
        private readonly StringComparison _comparison;

        /// <summary>
        /// Null until enumeration starts, which is what keeps a copy handed out by
        /// <see cref="GetEnumerator"/> independent of this one.
        /// </summary>
        private XmlElementEnumerator[]? _levels;
        private int _depth;

        public XmlDescendantElementEnumerator(SyntaxList<SyntaxNode> rootContent)
            : this(rootContent, localName: null, prefix: null, matchAnyPrefix: false, StringComparison.Ordinal)
        {
        }

        public XmlDescendantElementEnumerator(
            SyntaxList<SyntaxNode> rootContent,
            string? localName,
            string? prefix,
            bool matchAnyPrefix,
            StringComparison comparison)
        {
            _rootContent = rootContent;
            _localName = localName;
            _prefix = prefix;
            _matchAnyPrefix = matchAnyPrefix;
            _comparison = comparison;
            _levels = null;
            _depth = 0;
            Current = null!;
        }

        public XmlDescendantElementEnumerator GetEnumerator()
        {
            // A fresh descent, so enumerating twice does not share the level array.
            return new XmlDescendantElementEnumerator(_rootContent, _localName, _prefix, _matchAnyPrefix, _comparison);
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
        /// The first matching descendant. Throws when there is none.
        /// </summary>
        public XmlElementBaseSyntax First()
        {
            return FirstOrDefault() ?? throw new InvalidOperationException("Sequence contains no elements.");
        }

        /// <summary>
        /// The first matching descendant, or <c>null</c> when there is none.
        /// </summary>
        public XmlElementBaseSyntax? FirstOrDefault()
        {
            XmlDescendantElementEnumerator enumerator = GetEnumerator();

            return enumerator.MoveNext() ? enumerator.Current : null;
        }

        public bool MoveNext()
        {
            if (_levels is null)
            {
                _levels = new XmlElementEnumerator[InitialDepth];
                _levels[0] = new XmlElementEnumerator(_rootContent);
                _depth = 0;
            }

            while (true)
            {
                if (_levels[_depth].MoveNext())
                {
                    XmlElementBaseSyntax element = _levels[_depth].Current;

                    // Descend before reporting, so the children of a match are still visited.
                    Push(element);

                    if (Matches(element.NameNode))
                    {
                        Current = element;
                        return true;
                    }
                }
                else if (_depth == 0)
                {
                    Current = null!;
                    return false;
                }
                else
                {
                    _depth--;
                }
            }
        }

        /// <summary>
        /// With no local name the walk is unfiltered by name - but a prefix on its own still
        /// filters, rather than being quietly dropped.
        /// </summary>
        private bool Matches(XmlNameSyntax name)
        {
            if (_localName is not null)
            {
                return XmlNameMatcher.Matches(name, _localName, _prefix, _matchAnyPrefix, _comparison);
            }

            return _matchAnyPrefix
                || _prefix is null
                || XmlNameMatcher.PrefixMatches(name.Prefix, _prefix, _comparison);
        }

        private void Push(XmlElementBaseSyntax element)
        {
            _depth++;

            if (_depth == _levels!.Length)
            {
                Array.Resize(ref _levels, _levels.Length * 2);
            }

            _levels[_depth] = element.Elements;
        }

        public void Reset()
        {
            _levels = null;
            _depth = 0;
            Current = null!;
        }

        public XmlElementBaseSyntax Current { get; private set; }

        object IEnumerator.Current => Current;
    }
}
