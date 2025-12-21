using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Scriban;
using Scriban.Parsing;
using Scriban.Syntax;
using ScribanLanguage.Workspaces;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace ScribanLanguage.Servers;

public sealed class SemanticTokenServer(Workspace workspace, WorkspaceNavigator navigator, ILogger<SemanticTokenServer> rootLogger)
{
    private static LexerOptions GetOptions(Document document)
    {
        return document.LanguageId switch
        {
            "scriban" => ScriptOptions(),
            _ => TemplateOptions()
        };

        LexerOptions TemplateOptions()
        {
            return LexerOptions.Default;
        }

        LexerOptions ScriptOptions()
        {
            return new()
            {
                Mode = ScriptMode.ScriptOnly,
            };
        }
    }

    public Template ParseTextContent(TextContent content, Document document)
    {
        using var l = rootLogger.Function();
        var lexerOptions = GetOptions(document);
        l.Debug()?.Log("Parsing document {Uri} {Language}", document.Uri, document.LanguageId);
        return Template.Parse(
            content.OriginalString,
            document.Uri.OriginalString,
            lexerOptions: lexerOptions
        );
    }

    public ValueTask<SemanticTokens> SemanticTokens(SemanticTokensParams p, CancellationToken cancellationToken)
    {
        return SemanticTokensInternal(p.TextDocument.Uri, null, cancellationToken);
    }

    public ValueTask<SemanticTokens> SemanticTokensRange(SemanticTokensRangeParams p,
        CancellationToken cancellationToken)
    {
        return SemanticTokensInternal(p.TextDocument.Uri, p.Range, cancellationToken);
    }

    private async ValueTask<SemanticTokens> SemanticTokensInternal(Uri uri, Range? range,
        CancellationToken cancellationToken)
    {
        var host = await workspace.Host.Read(cancellationToken).ConfigureAwait(false);
        var options = host?.ServerCapabilities.SemanticTokensOptions ??
                      throw new InvalidOperationException("Host SemanticTokenOptions are uninitialized");
        var results = await navigator.ReadDocuments(static async (d, t, cancellationToken) =>
        {
            if (d.Uri != t.uri) return null;
            var template = await d.Template.Value(cancellationToken).ConfigureAwait(false);
            var visitor = new SemanticTokensScriptVisitor();
            template.Page.Accept(visitor);
            if (t.range is { } r)
            {
                var content = await d.Content.Read(cancellationToken).ConfigureAwait(false) ??
                              throw new InvalidOperationException($"Missing document content {d.Uri}");
                var start = content.GetOffset(r.Start);
                var end = content.GetOffset(r.End);
                visitor.Tokens.RemoveAll(x => x.Span.Start.Offset < start || x.Span.End.Offset > end);
            }

            return TokensToRelative(visitor.Tokens, d, t.options);
        }, (uri, range, options), cancellationToken).ConfigureAwait(false);
        return results.NotNull().FirstOrDefault() ?? new SemanticTokens();
    }

    private static SemanticTokens TokensToRelative(List<SemanticToken> tokens, Document document,
        SemanticTokensOptions options)
    {
        tokens.Sort();
        List<int> removeTokens = [];
        foreach (var ((token, next), tokenIndex) in tokens.Zip(tokens.Skip(1).Append(default)).Select((x, i) => (x, i)))
        {
            if (token == next)
            {
                removeTokens.Add(tokenIndex + 1);
            }
        }

        for (var index = removeTokens.Count - 1; index >= 0; index--)
        {
            var tokenIndex = removeTokens[index];
            tokens.RemoveAt(tokenIndex);
        }

        var serialized = new int[tokens.Count * 5];
        foreach (var ((prior, token), tokenIndex) in tokens.Prepend(default).Zip(tokens).Select((x, i) => (x, i)))
        {
            ref var data =
                ref Unsafe.As<int, SerializedSemanticToken>(
                    ref MemoryMarshal.GetReference(serialized.AsSpan(tokenIndex * 5, 5)));
            data.Line = token.Span.Start.Line - prior.Span.Start.Line;
            data.Col = data.Line == 0
                ? token.Span.Start.Column - prior.Span.Start.Column
                : token.Span.Start.Column;
            data.Len = token.Span.Length;
            data.Tt = options.Legend.TokenTypes.IndexOf(token.TokenType, StringComparer.Ordinal);
            data.Tm = token.TokenModifiers
                .Select(x => options.Legend.TokenModifiers.IndexOf(x, StringComparer.Ordinal))
                .Aggregate(0, (agg, b) => agg | (1 << b));
        }

        return new()
        {
            Data = serialized,
            ResultId = document.Version.ToString()
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SerializedSemanticToken
    {
        public int Line;
        public int Col;
        public int Len;
        public int Tt;
        public int Tm;
    }

    private sealed class SemanticTokensScriptVisitor : ScriptVisitor
    {
        public List<SemanticToken> Tokens { get; } = [];

        public override void Visit(ScriptArgumentBinary? node)
        {
            if (node is not null)
                Tokens.Add(new(node.Span, SemanticTokenTypes.Operator));
            base.Visit(node);
        }

        public override void Visit(ScriptAssignExpression? node)
        {
            if (node?.EqualToken is { } eq)
                Tokens.Add(new(eq.Span, SemanticTokenTypes.Operator));
            base.Visit(node);
        }

        public override void Visit(ScriptBinaryExpression? node)
        {
            if (node?.OperatorToken is { } op)
                Tokens.Add(new(op.Span, SemanticTokenTypes.Operator));
            base.Visit(node);
        }

        public override void Visit(ScriptConditionalExpression? node)
        {
            if (node?.QuestionToken is { } qt)
                Tokens.Add(new(qt.Span, SemanticTokenTypes.Operator));
            if (node?.ColonToken is { } ct)
                Tokens.Add(new(ct.Span, SemanticTokenTypes.Operator));
            base.Visit(node);
        }

        public override void Visit(ScriptKeyword? node)
        {
            if (node is not null)
                Tokens.Add(new(node.Span, SemanticTokenTypes.Keyword));
            base.Visit(node);
        }

        public override void Visit(ScriptLiteral? node)
        {
            if (node is null) return;
            Tokens.AddRange(node.StringTokenType switch
            {
                TokenType.BeginInterpolatedString =>
                [
                    new(node.Span.Slice(..^1), SemanticTokenTypes.String)
                ],
                TokenType.ContinuationInterpolatedString =>
                [
                    new(node.Span.Slice(..0), SemanticTokenTypes.Operator),
                    new(node.Span.Slice(1..^1), SemanticTokenTypes.String),
                    new(node.Span.Slice(^1..), SemanticTokenTypes.Operator),
                ],
                TokenType.EndingInterpolatedString =>
                [
                    new(node.Span.Slice(..1), SemanticTokenTypes.Operator),
                    new(node.Span.Slice(1..), SemanticTokenTypes.String),
                ],
                _ when node.Value is string =>
                [
                    new(node.Span, SemanticTokenTypes.String)
                ],
                _ => []
            });

            base.Visit(node);
        }

        public override void Visit(ScriptFunction? node)
        {
            if (node?.EqualToken is { } et)
                Tokens.Add(new(et.Span, SemanticTokenTypes.Operator));
            if (node?.NameOrDoToken is { } name)
                Tokens.Add(new(name.Span,
                    node.IsAnonymous ? SemanticTokenTypes.Keyword : SemanticTokenTypes.Function,
                    node.IsAnonymous ? [] : [SemanticTokenModifiers.Declaration]));
            base.Visit(node);
        }

        public override void Visit(ScriptIncrementDecrementExpression? node)
        {
            if (node?.OperatorToken is { } ot)
                Tokens.Add(new(ot.Span, SemanticTokenTypes.Operator));
            base.Visit(node);
        }

        public override void Visit(ScriptUnaryExpression? node)
        {
            if (node?.OperatorToken is { } ot)
                Tokens.Add(new(ot.Span, SemanticTokenTypes.Operator));
            base.Visit(node);
        }

        public override void Visit(ScriptIsEmptyExpression? node)
        {
            if (node?.QuestionToken is { } qt)
                Tokens.Add(new(qt.Span, SemanticTokenTypes.Operator));
            if (node?.DotToken is { } dt)
                Tokens.Add(new(dt.Span, SemanticTokenTypes.Operator));
            base.Visit(node);
        }

        public override void Visit(ScriptMemberExpression? node)
        {
            if (node?.DotToken is { } dt)
                Tokens.Add(new(dt.Span, SemanticTokenTypes.Operator));

            if (node?.Member is { } member)
                Tokens.Add(new(member.Span, SemanticTokenTypes.Property));
            base.Visit(node);
        }

        public override void Visit(ScriptNamedArgument? node)
        {
            if (node?.Name is { } name)
                Tokens.Add(new(name.Span, SemanticTokenTypes.Parameter));
            if (node?.ColonToken is { } ct)
                Tokens.Add(new(ct.Span, SemanticTokenTypes.Operator));
            base.Visit(node);
        }

        public override void Visit(ScriptParameter? node)
        {
            if (node?.Name is { } name)
                Tokens.Add(new(name.Span, SemanticTokenTypes.Parameter));
            if (node?.EqualOrTripleDotToken is { } eq)
                Tokens.Add(new(eq.Span, SemanticTokenTypes.Operator));
            base.Visit(node);
        }

        public override void Visit(ScriptObjectMember? node)
        {
            if (node?.Name is { } name)
                Tokens.Add(new(name.Span, SemanticTokenTypes.Property));
            base.Visit(node);
        }

        public override void Visit(ScriptVariableGlobal? node)
        {
            if (node is not null)
                Tokens.Add(new(node.Span, SemanticTokenTypes.Variable, [SemanticTokenModifiers.Static]));
            base.Visit(node);
        }

        public override void Visit(ScriptVariableLocal? node)
        {
            if (node is not null)
                Tokens.Add(new(node.Span, SemanticTokenTypes.Variable));
            base.Visit(node);
        }

        public override void Visit(ScriptIndexerExpression? node)
        {
            if (node?.OpenBracket is { } ob)
                Tokens.Add(new(ob.Span, SemanticTokenTypes.Operator));
            if (node?.CloseBracket is { } cb)
                Tokens.Add(new(cb.Span, SemanticTokenTypes.Operator));
            base.Visit(node);
        }

        public override void Visit(ScriptInterpolatedExpression? node)
        {
            if (node?.OpenBrace is { } ob)
                Tokens.Add(new(ob.Span, SemanticTokenTypes.Operator));
            if (node?.CloseBrace is { } cb)
                Tokens.Add(new(cb.Span, SemanticTokenTypes.Operator));
            base.Visit(node);
        }
    }

    [DebuggerDisplay("{ToString(),nq}")]
    private readonly struct SemanticToken(
        SourceSpan span,
        string tokenType,
        IReadOnlyList<string>? tokenModifiers = null) : IComparable<SemanticToken>, IEquatable<SemanticToken>
    {
        public SourceSpan Span => span;
        public string TokenType => tokenType;
        public IReadOnlyList<string> TokenModifiers => tokenModifiers ?? [];

        public int CompareTo(SemanticToken other)
        {
            return Span.Start.Offset.CompareTo(other.Span.Start.Offset);
        }

        public bool Equals(SemanticToken other)
        {
            return Span.Start.Offset == other.Span.Start.Offset &&
                   Span.End.Offset == other.Span.End.Offset;
        }

        public override bool Equals(object? obj)
        {
            return obj is SemanticToken other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Span.Start.Offset, Span.End.Offset);
        }

        public static bool operator ==(SemanticToken lhs, SemanticToken rhs) => lhs.Equals(rhs);
        public static bool operator !=(SemanticToken lhs, SemanticToken rhs) => !(lhs == rhs);

        public override string ToString()
        {
            return $"{span.Start}..{span.End} {tokenType} [{tokenModifiers?.JoinBy(",")}]";
        }
    }
}