﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.ConvertIfToSwitch
{
    using static SyntaxFactory;

    internal sealed partial class CSharpConvertIfToSwitchCodeRefactoringProvider
    {
        private static readonly Dictionary<BinaryOperatorKind, SyntaxKind> s_operatorMap = new Dictionary<BinaryOperatorKind, SyntaxKind>
        {
            { BinaryOperatorKind.NotEquals, SyntaxKind.ExclamationEqualsToken },
            { BinaryOperatorKind.LessThan, SyntaxKind.LessThanToken },
            { BinaryOperatorKind.GreaterThan,  SyntaxKind.GreaterThanToken },
            { BinaryOperatorKind.LessThanOrEqual,  SyntaxKind.LessThanEqualsToken },
            { BinaryOperatorKind.GreaterThanOrEqual,  SyntaxKind.GreaterThanEqualsToken },
        };

        public override SyntaxNode CreateSwitchExpressionStatement(SyntaxNode target, ImmutableArray<AnalyzedSwitchSection> sections)
        {
            return ReturnStatement(
                SwitchExpression(
                    (ExpressionSyntax)target,
                    SeparatedList(sections.Select(AsSwitchExpressionArmSyntax))));
        }

        private static SwitchExpressionArmSyntax AsSwitchExpressionArmSyntax(AnalyzedSwitchSection section)
        {
            if (section.Labels.IsDefault)
                return SwitchExpressionArm(DiscardPattern(), AsExpressionSyntax(section.Body));

            var pattern = AsPatternSyntax(section.Labels[0].Pattern);
            var whenClause = AsWhenClause(section.Labels[0]);

            Debug.Assert(whenClause == null || section.Labels.Length == 1, "We shouldn't have guards when we're combining multiple cases into a single arm");

            for (int i = 1; i < section.Labels.Length; i++)
            {
                var label = section.Labels[i];
                Debug.Assert(label.Guards.Length == 0, "We shouldn't have guards when we're combining multiple cases into a single arm");
                var nextPattern = AsPatternSyntax(label.Pattern);
                pattern = BinaryPattern(SyntaxKind.OrPattern, pattern, nextPattern);
            }

            return SwitchExpressionArm(pattern, whenClause, AsExpressionSyntax(section.Body));
        }

        private static ExpressionSyntax AsExpressionSyntax(IOperation operation)
            => operation switch
            {
                IReturnOperation op => (ExpressionSyntax)op.ReturnedValue.Syntax,
                IThrowOperation op => ThrowExpression((ExpressionSyntax)op.Exception.Syntax),
                IBlockOperation op => AsExpressionSyntax(op.Operations.Single()),
                var v => throw ExceptionUtilities.UnexpectedValue(v.Kind)
            };

        public override SyntaxNode CreateSwitchStatement(IfStatementSyntax ifStatement, SyntaxNode expression, IEnumerable<SyntaxNode> sectionList)
        {
            var block = ifStatement.Statement as BlockSyntax;
            return SwitchStatement(
                switchKeyword: Token(SyntaxKind.SwitchKeyword).WithTriviaFrom(ifStatement.IfKeyword),
                openParenToken: ifStatement.OpenParenToken,
                expression: (ExpressionSyntax)expression,
                closeParenToken: ifStatement.CloseParenToken.WithPrependedLeadingTrivia(ElasticMarker),
                openBraceToken: block?.OpenBraceToken ?? Token(SyntaxKind.OpenBraceToken),
                sections: List(sectionList.Cast<SwitchSectionSyntax>()),
                closeBraceToken: block?.CloseBraceToken ?? Token(SyntaxKind.CloseBraceToken));
        }

        private static WhenClauseSyntax? AsWhenClause(AnalyzedSwitchLabel label)
            => AsWhenClause(label.Guards
                .Select(e => e.WalkUpParentheses())
                .AggregateOrDefault((prev, current) => BinaryExpression(SyntaxKind.LogicalAndExpression, current, prev)));

        private static WhenClauseSyntax? AsWhenClause(ExpressionSyntax? expression)
            => expression is null ? null : WhenClause(expression);

        public override SyntaxNode AsSwitchLabelSyntax(AnalyzedSwitchLabel label)
            => CasePatternSwitchLabel(
                AsPatternSyntax(label.Pattern),
                AsWhenClause(label),
                Token(SyntaxKind.ColonToken));

        private static PatternSyntax AsPatternSyntax(AnalyzedPattern pattern)
            => pattern switch
            {
                AnalyzedPattern.And p => BinaryPattern(SyntaxKind.AndPattern, AsPatternSyntax(p.LeftPattern), AsPatternSyntax(p.RightPattern)),
                AnalyzedPattern.Constant p => ConstantPattern(p.ExpressionSyntax),
                AnalyzedPattern.Source p => p.PatternSyntax,
                AnalyzedPattern.Type p => DeclarationPattern((TypeSyntax)p.IsExpressionSyntax.Right, DiscardDesignation()),
                AnalyzedPattern.Relational p => RelationalPattern(Token(s_operatorMap[p.OperatorKind]), p.Value),
                AnalyzedPattern.Range p => BinaryPattern(
                    SyntaxKind.AndPattern,
                    RelationalPattern(Token(SyntaxKind.GreaterThanEqualsToken), p.LowerBound),
                    RelationalPattern(Token(SyntaxKind.LessThanEqualsToken), p.HigherBound)),
                var p => throw ExceptionUtilities.UnexpectedValue(p)
            };

        public override IEnumerable<SyntaxNode> AsSwitchSectionStatements(IOperation operation)
        {
            var node = operation.Syntax;
            var requiresBreak = operation.SemanticModel.AnalyzeControlFlow(node).EndPointIsReachable;
            var requiresBlock = !operation.SemanticModel.AnalyzeDataFlow(node).VariablesDeclared.IsDefaultOrEmpty;

            var statements = ArrayBuilder<SyntaxNode>.GetInstance();
            if (node is BlockSyntax block)
            {
                if (block.Statements.Count == 0)
                {
                    statements.Add(BreakStatement());
                }
                else if (requiresBlock)
                {
                    statements.Add(requiresBreak ? block.AddStatements(BreakStatement()) : block);
                }
                else
                {
                    statements.AddRange(block.Statements);
                    if (requiresBreak)
                    {
                        statements.Add(BreakStatement());
                    }
                }
            }
            else
            {
                statements.Add(node);
                if (requiresBreak)
                {
                    statements.Add(BreakStatement());
                }
            }

            return statements.ToArrayAndFree();
        }
    }
}
