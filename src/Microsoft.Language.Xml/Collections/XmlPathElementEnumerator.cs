using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Language.Xml.Collections
{
    /// <summary>
    /// Depth-first walk of every element reachable by a slash-separated child path. Every segment is
    /// expanded, so a path crossing several same-named ancestors yields the matches under all of them.
    /// </summary>
    /// <remarks>
    /// The path is never split into strings: segments are compared as spans over the original path,
    /// so no per-segment string is allocated. Enumerating allocates one array to hold the descent,
    /// sized to the number of segments; nothing is allocated per element visited.
    /// <br/>
    /// That array is what a walk is made of, so two enumerators cannot share one. Copying this
    /// struct after enumeration has started - assigning it to a second variable, passing it by
    /// value - hands out a second reader of the same walk, and advancing either moves both.
    /// <see cref="GetEnumerator"/>, <see cref="Reset"/> and <see cref="FirstOrDefault"/> all start
    /// a fresh one, so <c>foreach</c> and the LINQ operators are unaffected; a raw copy taken
    /// mid-walk is the one thing to avoid.
    /// </remarks>
    public struct XmlPathElementEnumerator : IEnumerable<XmlElementBaseSyntax>, IEnumerator<XmlElementBaseSyntax>
    {
        /// <summary>
        /// One level of the descent: the children being walked, and where in the path the name they
        /// must match lives.
        /// </summary>
        private struct Level
        {
            public XmlElementEnumerator Elements;
            public int SegmentStart;
            public int SegmentLength;
        }

        /// <summary>
        /// The nodes the first segment is matched against. Held as content rather than as an
        /// element so a document, whose root element is the first segment, can be walked too.
        /// </summary>
        private readonly SyntaxList<SyntaxNode> _rootContent;

        private readonly string _path;
        private readonly int _segmentCount;

        /// <summary>
        /// Where the first segment starts - 1 when the path was written with a leading slash.
        /// </summary>
        private readonly int _start;

        /// <summary>
        /// Null until enumeration starts, which is what keeps a copy handed out by
        /// <see cref="GetEnumerator"/> independent of this one.
        /// </summary>
        private Level[]? _levels;
        private int _depth;

        internal XmlPathElementEnumerator(SyntaxList<SyntaxNode> rootContent, string path)
        {
            _rootContent = rootContent;
            _path = path;
            // Validating in the constructor rather than during the walk means a malformed path
            // throws from the call that built it.
            _start = XmlPath.Validate(path, nameof(path), out _segmentCount);
            _levels = null;
            _depth = 0;
            Current = null!;
        }

        private Level CreateLevel(SyntaxList<SyntaxNode> content, int segmentStart)
        {
            var end = _path.IndexOf('/', segmentStart);

            return new Level
            {
                Elements = new XmlElementEnumerator(content),
                SegmentStart = segmentStart,
                SegmentLength = (end < 0 ? _path.Length : end) - segmentStart
            };
        }

        /// <summary>
        /// Advances the level's children to the next one whose name matches the level's segment.
        /// </summary>
        private bool MoveToNextMatch(int depth)
        {
            ReadOnlySpan<char> segment = _path.AsSpan(_levels![depth].SegmentStart, _levels[depth].SegmentLength);
            var colon = segment.IndexOf(':');
            ReadOnlySpan<char> localName = colon < 0 ? segment : segment.Slice(colon + 1);
            ReadOnlySpan<char> prefix = colon < 0 ? default : segment.Slice(0, colon);

            while (_levels[depth].Elements.MoveNext())
            {
                XmlNameSyntax name = _levels[depth].Elements.Current.NameNode;

                if (!name.LocalName.AsSpan().SequenceEqual(localName))
                {
                    continue;
                }

                // A path segment without a prefix matches only unprefixed elements, matching the
                // string-based GetElement/GetElements overloads. A segment written with an empty
                // prefix says the same thing rather than matching nothing.
                if (prefix.IsEmpty
                        ? string.IsNullOrEmpty(name.Prefix)
                        : name.Prefix.AsSpan().SequenceEqual(prefix))
                {
                    return true;
                }
            }

            return false;
        }

        public XmlPathElementEnumerator GetEnumerator()
        {
            // A fresh descent, so enumerating twice does not share the level array.
            return new XmlPathElementEnumerator(_rootContent, _path);
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
        /// The first element on the path. Throws when there is none.
        /// </summary>
        public XmlElementBaseSyntax First()
        {
            return FirstOrDefault() ?? throw new InvalidOperationException("Sequence contains no elements.");
        }

        /// <summary>
        /// The first element on the path, or <c>null</c> when there is none.
        /// </summary>
        public XmlElementBaseSyntax? FirstOrDefault()
        {
            XmlPathElementEnumerator enumerator = GetEnumerator();

            return enumerator.MoveNext() ? enumerator.Current : null;
        }

        public bool MoveNext()
        {
            if (_levels is null)
            {
                _levels = new Level[_segmentCount];
                _levels[0] = CreateLevel(_rootContent, _start);
                _depth = 0;
            }

            while (true)
            {
                if (MoveToNextMatch(_depth))
                {
                    if (_depth == _segmentCount - 1)
                    {
                        Current = _levels[_depth].Elements.Current;
                        return true;
                    }

                    XmlElementBaseSyntax next = _levels[_depth].Elements.Current;
                    var segmentStart = _levels[_depth].SegmentStart + _levels[_depth].SegmentLength + 1;

                    _depth++;
                    _levels[_depth] = CreateLevel(next.Content, segmentStart);
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
