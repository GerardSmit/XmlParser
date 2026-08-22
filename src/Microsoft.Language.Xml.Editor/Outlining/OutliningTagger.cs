using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace Microsoft.Language.Xml.Editor
{
    public class OutliningTagger : AbstractSyntaxTreeTagger, ITagger<IOutliningRegionTag>
    {
        private static readonly IEnumerable<ITagSpan<IOutliningRegionTag>> emptyTagList = Enumerable.Empty<ITagSpan<IOutliningRegionTag>>();

        private ITextBuffer buffer;
        private OutliningTaggerProvider outliningTaggerProvider;

        private XmlNodeSyntax lastRoot;
        private ITextSnapshot lastRootSnapshot;

        public OutliningTagger(OutliningTaggerProvider outliningTaggerProvider, ITextBuffer buffer)
            : base(outliningTaggerProvider.ParserService)
        {
            this.outliningTaggerProvider = outliningTaggerProvider;
            this.buffer = buffer;
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
            {
                return emptyTagList;
            }

            var snapshot = spans[0].Snapshot;

            var task = parserService.GetSyntaxTree(snapshot);

            // wait for 100 milliseconds to see if we're lucky and it finishes before that
            // this helps significantly reduce flicker since we're not going to clear and re-add all tags on every keystroke
            task.Wait(100);

            if (task.Status == TaskStatus.RanToCompletion)
            {
                lastRoot = task.Result;
                lastRootSnapshot = snapshot;
            }
            else
            {
                task.ContinueWith(t =>
                {
                    lastRoot = t.Result;
                    lastRootSnapshot = snapshot;
                    RaiseTagsChanged(snapshot);
                }, TaskContinuationOptions.OnlyOnRanToCompletion);
            }

            // While a parse for a newer snapshot is still running, keep serving the tags from
            // the last completed parse, translated forward. Returning an empty list here makes
            // the outlining manager treat all collapsed regions in the queried spans as removed
            // and expand them.
            var root = lastRoot;
            var rootSnapshot = lastRootSnapshot;
            if (root == null || rootSnapshot == null)
            {
                return emptyTagList;
            }

            var elementSpans = new List<Tuple<Span, string>>();
            CollectElementSpans(root, elementSpans, 0);
            var tagSpans = new List<TagSpan<IOutliningRegionTag>>();
            int previousStartLine = -1;
            foreach (var span in elementSpans)
            {
                if (span.Item1.End > rootSnapshot.Length)
                {
                    continue;
                }

                int startLine = rootSnapshot.GetLineNumberFromPosition(span.Item1.Start);
                if (startLine >= rootSnapshot.GetLineNumberFromPosition(span.Item1.End))
                {
                    continue;
                }

                // nested elements starting on the same line: the outer one is enough
                if (startLine == previousStartLine)
                {
                    continue;
                }

                previousStartLine = startLine;

                var tagSnapshotSpan = new SnapshotSpan(rootSnapshot, span.Item1).TranslateTo(snapshot, SpanTrackingMode.EdgeExclusive);
                tagSpans.Add(new TagSpan<IOutliningRegionTag>(
                    tagSnapshotSpan,
                    new OutliningRegionTag(span.Item2, span.Item2)));
            }

            return tagSpans;
        }

        private void CollectElementSpans(SyntaxNode node, List<Tuple<Span, string>> spans, int start)
        {
            if (node is XmlElementBaseSyntax)
            {
                var leading = node.GetLeadingTriviaWidth();
                var trailing = node.GetTrailingTriviaWidth();
                spans.Add(Tuple.Create(
                    new Span(start + leading, node.FullWidth - leading - trailing),
                    "<" + (node as XmlElementBaseSyntax).Name + ">"));
            }

            foreach (var child in node.ChildNodes)
            {
                CollectElementSpans(child, spans, start);
                start += child.FullWidth;
            }
        }

        private void RaiseTagsChanged(ITextSnapshot snapshot)
        {
            TagsChanged?.Invoke(
                this,
                new SnapshotSpanEventArgs(
                    new SnapshotSpan(snapshot, 0, snapshot.Length)));
        }
    }
}
