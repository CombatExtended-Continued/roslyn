﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.CodeActions;

namespace Microsoft.CodeAnalysis.CSharp.ConvertToRawString;

internal readonly struct CanConvertParams(CodeActionPriority priority, bool canBeSingleLine, bool canBeMultiLineWithoutLeadingWhiteSpaces)
{
    public CodeActionPriority Priority { get; } = priority;
    public bool CanBeSingleLine { get; } = canBeSingleLine;
    public bool CanBeMultiLineWithoutLeadingWhiteSpaces { get; } = canBeMultiLineWithoutLeadingWhiteSpaces;
}
