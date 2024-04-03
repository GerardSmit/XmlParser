using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Language.Xml.Collections
{
    public struct XmlAttributeEnumerator(
        SyntaxList<XmlAttributeSyntax> content
    ) : IEnumerable<KeyValuePair<string, string>>, IEnumerator<KeyValuePair<string, string>>
    {
        private int _current;

        public XmlAttributeEnumerator GetEnumerator()
        {
            return this;
        }

        IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
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
            if (content.Node is XmlAttributeSyntax singleAttribute)
            {
                if (_current > 0)
                {
                    Current = default;
                    return false;
                }

                Current = new KeyValuePair<string, string>(singleAttribute.Name, singleAttribute.Value);
                return true;
            }

            if (_current >= content.Count)
            {
                Current = default;
                return false;
            }

            XmlAttributeSyntax attribute = content[_current];
            Current = new KeyValuePair<string, string>(attribute.Name, attribute.Value);
            _current++;
            return true;
        }

        public void Reset()
        {
            _current = 0;
        }

        public KeyValuePair<string, string> Current { get; private set; }

        object IEnumerator.Current => Current;
    }
}
