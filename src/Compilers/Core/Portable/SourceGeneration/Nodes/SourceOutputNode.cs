﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.PooledObjects;

using TOutput = System.ValueTuple<System.Collections.Generic.IEnumerable<Microsoft.CodeAnalysis.GeneratedSourceText>, System.Collections.Generic.IEnumerable<Microsoft.CodeAnalysis.Diagnostic>>;

namespace Microsoft.CodeAnalysis
{
    internal sealed class SourceOutputNode<TInput> : IIncrementalGeneratorOutputNode, IIncrementalGeneratorNode<TOutput>
    {
        private readonly IIncrementalGeneratorNode<TInput> _source;

        private readonly Action<SourceProductionContext, TInput> _action;

        public SourceOutputNode(IIncrementalGeneratorNode<TInput> source, Action<SourceProductionContext, TInput> action)
        {
            _source = source;
            _action = action;
        }

        public NodeStateTable<TOutput> UpdateStateTable(DriverStateTable.Builder graphState, NodeStateTable<TOutput> previousTable, CancellationToken cancellationToken)
        {
            // PROTOTYPE(source-generators):caching, faulted etc. need to extract out the common logic 

            var nodeTable = new NodeStateTable<TOutput>.Builder();

            var sourceTable = graphState.GetLatestStateTableForNode(_source);
            foreach (var entry in sourceTable)
            {
                if (entry.state == EntryState.Cached || entry.state == EntryState.Removed)
                {
                    nodeTable.AddEntriesFromPreviousTable(previousTable, entry.state);
                }
                else if (entry.state == EntryState.Added || entry.state == EntryState.Modified)
                {
                    // TODO: handle modified properly

                    var sourcesBuilder = ArrayBuilder<GeneratedSourceText>.GetInstance();
                    var diagnostics = DiagnosticBag.GetInstance();

                    SourceProductionContext context = new SourceProductionContext(sourcesBuilder, diagnostics, cancellationToken);
                    try
                    {
                        _action(context, entry.item);
                        // PROTOTYPE(source-generators):
                        nodeTable.AddEntries(ImmutableArray.Create<TOutput>((sourcesBuilder.ToImmutable(), diagnostics.ToReadOnly())), EntryState.Added);
                    }
                    finally
                    {
                        sourcesBuilder.Free();
                        diagnostics.Free();
                    }
                }
            }

            return nodeTable.ToImmutableAndFree();
        }

        // PROTOTYPE(source-generators):
        public IIncrementalGeneratorNode<TOutput> WithComparer(IEqualityComparer<TOutput> comparer) { return this; }

        public void AppendOutputs(IncrementalExecutionContext context)
        {
            // get our own state table
            var table = context.TableBuilder.GetLatestStateTableForNode(this);

            // add each non-removed entry to the context
            foreach (var ((sources, diagnostics), state) in table)
            {
                if (state != EntryState.Removed)
                {
                    foreach (var text in sources)
                    {
                        try
                        {
                            context.Sources.Add(text.HintName, text.Text);
                        }
                        catch (ArgumentException e)
                        {
                            //PROTOTYPE(source-generators): we should update the error messages to be specific about *which* file errored as it now won't happen
                            //                              at the same time the file is added.
                            throw new UserFunctionException(e);
                        }
                    }
                    context.Diagnostics.AddRange(diagnostics);
                }
            }

        }
    }
}
