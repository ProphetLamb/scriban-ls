using Microsoft.VisualStudio.LanguageServer.Protocol;
using Scriban.Parsing;
using Range = System.Range;

namespace ScribanLanguage.Extensions;

public static class ScribanExtensions
{
    public static Position ToPosition(this TextPosition pos)
    {
        return new(pos.Line, pos.Column);
    }

    public static DiagnosticSeverity ToSeverity(this ParserMessageType t)
    {
        return t switch
        {
            ParserMessageType.Error => DiagnosticSeverity.Error,
            ParserMessageType.Warning => DiagnosticSeverity.Warning,
            _ => DiagnosticSeverity.Information
        };
    }

    public static SourceSpan Slice(this SourceSpan span, Range range)
    {
        return new(
            span.FileName,
            range.Start.IsFromEnd
                ? new(span.End.Offset - range.Start.Value, span.End.Line, span.End.Column - range.Start.Value)
                : new(span.Start.Offset + range.Start.Value, span.Start.Line, span.Start.Column + range.Start.Value),
            range.End.IsFromEnd
                ? new(span.End.Offset - range.End.Value, span.End.Line, span.End.Column - range.End.Value)
                : new(span.Start.Offset + range.End.Value, span.Start.Line, span.Start.Column + range.End.Value)
        );
    }
}