﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

namespace Microsoft.CodeAnalysis
{
    // PROTOTYPE(source-generators):
    // the builder seems useful as a type, but the actual table is just a wrapper around a dict
    // do we actually need the type itself, or should we just store the dict directly as
    // part of the driver state?
    internal sealed class DriverStateTable
    {
        // PROTOTYPE(source-generators): should we make a non generic node interface that we can use as the key
        //                               instead of just object?
        private readonly ImmutableDictionary<object, IStateTable> _tables;

        internal static DriverStateTable Empty { get; } = new DriverStateTable(ImmutableDictionary<object, IStateTable>.Empty);

        private DriverStateTable(ImmutableDictionary<object, IStateTable> tables)
        {
            _tables = tables;
        }

        internal NodeStateTable<T> GetStateTable<T>(IIncrementalGeneratorNode<T> input) => _tables.ContainsKey(input) ? (NodeStateTable<T>)_tables[input] : NodeStateTable<T>.Empty;

        internal DriverStateTable SetStateTable<T>(IIncrementalGeneratorNode<T> input, NodeStateTable<T> table) => new DriverStateTable(_tables.SetItem(input, table));

        public sealed class Builder
        {
            private readonly ImmutableDictionary<object, IStateTable>.Builder _tableBuilder = ImmutableDictionary.CreateBuilder<object, IStateTable>();

            private readonly DriverStateTable _previousTable;

            private readonly CancellationToken _cancellationToken;

            public Builder(DriverStateTable previousTable, CancellationToken cancellationToken = default)
            {
                _previousTable = previousTable;
                _cancellationToken = cancellationToken;
            }

            public void SetInputState<T>(InputNode<T> source, NodeStateTable<T> state)
            {
                _tableBuilder[source] = state;
            }

            public NodeStateTable<T> GetLatestStateTableForNode<T>(IIncrementalGeneratorNode<T> source)
            {
                // if we've already evaluated a node during this build, we can just return the existing result
                if (_tableBuilder.ContainsKey(source))
                {
                    return (NodeStateTable<T>)_tableBuilder[source];
                }

                // get the previous table, if there was one for this node
                NodeStateTable<T> previousTable = _previousTable.GetStateTable(source);

                // request the node update its state based on the current driver table and store the new result
                var newTable = source.UpdateStateTable(this, previousTable, _cancellationToken);
                _tableBuilder[source] = newTable;
                return newTable;
            }

            // PROTOTYPE: we only test this, but it'll eventually be called as part of saving the state back to the GeneratorDriverState
            public DriverStateTable ToImmutable()
            {
                // we can compact the tables at this point, as we'll no longer be using them to determine current state
                var keys = _tableBuilder.Keys.ToArray();
                foreach (var key in keys)
                {
                    _tableBuilder[key] = _tableBuilder[key].Compact();
                }

                return new DriverStateTable(_tableBuilder.ToImmutable());
            }
        }
    }
}
