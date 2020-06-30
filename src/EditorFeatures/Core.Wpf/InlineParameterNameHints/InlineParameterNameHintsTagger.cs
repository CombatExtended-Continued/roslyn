﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace Microsoft.CodeAnalysis.Editor.InlineParameterNameHints
{
    /// <summary>
    /// The purpose of this tagger is to convert the <see cref="InlineParameterNameHintDataTag"/> to
    /// the <see cref="InlineParameterNameHintsTag"/>, which actually creates the UIElement. It reacts to
    /// tags changing and updates the adornments accordingly.
    /// </summary>
    internal sealed class InlineParameterNameHintsTagger : ITagger<IntraTextAdornmentTag>, IDisposable
    {
        private readonly ITagAggregator<InlineParameterNameHintDataTag> _tagAggregator;
        private readonly ITextBuffer _buffer;

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public InlineParameterNameHintsTagger(ITextBuffer buffer, ITagAggregator<InlineParameterNameHintDataTag> tagAggregator)
        {
            _buffer = buffer;
            _tagAggregator = tagAggregator;
            _tagAggregator.TagsChanged += OnTagAggregatorTagsChanged;
        }

        private void OnTagAggregatorTagsChanged(object sender, TagsChangedEventArgs e)
        {
            var spans = e.Span.GetSpans(_buffer);
            foreach (var span in spans)
            {
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
            }
        }

        public IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count <= 0)
            {
                return Array.Empty<ITagSpan<IntraTextAdornmentTag>>();
            }

            var tagsList = new List<ITagSpan<IntraTextAdornmentTag>>();
            var dataTags = _tagAggregator.GetTags(spans);
            var snapshot = spans[0].Snapshot;
            foreach (var tag in dataTags)
            {
                // Gets the associated span from the snapshot span and creates the IntraTextAdornmentTag from the data
                // tags. Only dealing with the dataTagSpans if the count is 1 because we do not see a multi-buffer case
                // occuring 
                var dataTagSpans = tag.Span.GetSpans(snapshot);
                var textTag = tag.Tag;
                if (dataTagSpans.Count == 1)
                {
                    var dataTagSpan = dataTagSpans[0];
                    tagsList.Add(new TagSpan<IntraTextAdornmentTag>(new SnapshotSpan(dataTagSpan.Start, 0), new InlineParameterNameHintsTag(textTag.ParameterName)));
                }
            }

            return tagsList;
        }

        public void Dispose()
        {
            _tagAggregator.Dispose();
            _tagAggregator.TagsChanged -= OnTagAggregatorTagsChanged;
        }
    }
}
