using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace ScribanLanguage.Workspaces;

public sealed record TextContent(string OriginalString, IReadOnlyList<int> LineIndices)
{
    public static TextContent Empty { get; } = new("", [0]);

    public static TextContent Parse(string? text)
    {
        return new(text ?? "", ComputeLineIndices(text));
    }

    public ReadOnlySpan<char> Line(int lineIndex)
    {
        return lineIndex + 1 < LineIndices.Count
            ? OriginalString[LineIndices[lineIndex]..LineIndices[lineIndex + 1]]
            : OriginalString[LineIndices[lineIndex]..];
    }

    public int GetOffset(Position pos) => GetOffset(pos.Line, pos.Character);

    public int GetOffset(int line, int column)
    {
        var num = LineIndices[line];
        return num + column;
    }

    public (int Line, int Column) GetLineAndColumn(int index)
    {
        var lo = 0;
        var hi = LineIndices.Count - 1;
        var pivot = 0;
        while (lo <= hi)
        {
            pivot = (lo + hi) / 2;
            var lineOffset = LineIndices[pivot];
            if (lineOffset < index)
            {
                lo = pivot + 1;
                continue;
            }

            if (lineOffset > index)
            {
                hi = pivot - 1;
                continue;
            }

            break;
        }

        var line = lo <= hi ? pivot : hi;
        return (line, index - LineIndices[line]);
    }

    private static List<int> ComputeLineIndices(ReadOnlySpan<char> code)
    {
        var list = new List<int>();
        var offset = 0;
        list.Add(offset);
        var length = code.Length;
        for (var i = 0; i < length; i++)
        {
            if (offset >= length)
            {
                break;
            }

            var c = code[offset];
            if (c == '\r')
            {
                if (offset + 1 >= length)
                {
                    break;
                }

                if (code[offset + 1] == '\n')
                {
                    offset += 2;
                    list.Add(offset);
                }
                else
                {
                    offset++;
                    list.Add(offset);
                }
            }
            else if (c == '\n')
            {
                offset++;
                list.Add(offset);
            }
            else
            {
                offset++;
            }

            if (offset >= length)
            {
                break;
            }
        }

        return list;
    }
}