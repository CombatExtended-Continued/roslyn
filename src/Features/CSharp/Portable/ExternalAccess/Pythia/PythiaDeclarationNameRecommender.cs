﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp.Completion.Providers.DeclarationName;
using Microsoft.CodeAnalysis.CSharp.Extensions.ContextQuery;
using Microsoft.CodeAnalysis.CSharp.ExternalAccess.Pythia.Api;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Microsoft.CodeAnalysis.CSharp.ExternalAccess.Pythia
{
    [ExportDeclarationNameRecommender(nameof(PythiaDeclarationNameRecommender)), Shared]
    [ExtensionOrder(Before = nameof(DeclarationNameRecommender))]
    internal sealed class PythiaDeclarationNameRecommender : IDeclarationNameRecommender
    {
        private readonly Lazy<IPythiaDeclarationNameRecommenderImplmentation>? _lazyImplementation;

        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public PythiaDeclarationNameRecommender([Import(AllowDefault = true)] Lazy<IPythiaDeclarationNameRecommenderImplmentation>? implementation)
            => _lazyImplementation = implementation;

        public async Task<ImmutableArray<(string name, Glyph glyph)>> ProvideRecommendedNamesAsync(
            CompletionContext completionContext,
            Document document,
            CSharpSyntaxContext syntaxContext,
            NameDeclarationInfo nameInfo,
            CancellationToken cancellationToken)
        {
            if (_lazyImplementation is null || nameInfo.PossibleSymbolKinds.IsEmpty)
                return ImmutableArray<(string, Glyph)>.Empty;

            var context = new PythiaDeclarationNameContext(syntaxContext);

            var _ = ArrayBuilder<(string, Glyph)>.GetInstance(out var builder);
            var result = await _lazyImplementation.Value.ProvideRecommendationsAsync(context, cancellationToken).ConfigureAwait(false);
            // We just pick the first possible symbol kind for glyph.
            builder.AddRange(result.Select(name
                => (name, NameDeclarationInfo.GetGlyph(NameDeclarationInfo.GetSymbolKind(nameInfo.PossibleSymbolKinds[0]), nameInfo.DeclaredAccessibility))));

            return builder.ToImmutable();
        }
    }
}
