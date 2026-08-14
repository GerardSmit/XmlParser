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

        private readonly XmlElementBaseSyntax _root;
        private readonly string _path;
        private readonly int _segmentCount;

        /// <summary>
        /// Null until enumeration starts, which is what keeps a copy handed out by
        /// <see cref="GetEnumerator"/> independent of this one.
        /// </summary>
        private Level[]? _levels;
        private int _depth;

        internal XmlPathElementEnumerator(XmlElementBaseSyntax root, string path)
        {
            _root = root;
            _path = path;
            _segmentCount = CountSegments(path);
            _levels = null;
            _depth = 0;
            Current = null!;
        }

        /// <summary>
        /// Counts the segments, rejecting an empty path or an empty segment. Validating here rather
        /// than during the walk means a malformed path throws from the call that built it.
        /// </summary>
        private static int CountSegments(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path must not be empty.", nameof(path));
            }

            var count = 1;

            for (var i = 0; i < path.Length; i++)
            {
                if (path[i] != '/')
                {
                    continue;
                }

                if (i == 0 || i == path.Length - 1 || path[i - 1] == '/')
                {
                    throw new ArgumentException($"Path '{path}' contains an empty segment.", nameof(path));
                }

                count++;
            }

            return count;
        }

        private Level CreateLevel(XmlElementBaseSyntax parent, int segmentStart)
        {
            var end = _path.IndexOf('/', segmentStart);

            return new Level
            {
                Elements = parent.Elements,
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
                // string-based GetElement/GetElements overloads.
                if (colon < 0
                        ? name.Prefix is null
                        : name.Prefix is not null && name.Prefix.AsSpan().SequenceEqual(prefix))
                {
                    return true;
                }
            }

            return false;
        }

        public XmlPathElementEnumerator GetEnumerator()
        {
            // A fresh descent, so enumerating twice does not share the level array.
            return new XmlPathElementEnumerator(_root, _path);
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
                _levels[0] = CreateLevel(_root, 0);
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
                    _levels[_depth] = CreateLevel(next, segmentStart);
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
