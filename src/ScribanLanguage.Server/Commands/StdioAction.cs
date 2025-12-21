using System.CommandLine;
using StreamJsonRpc;

namespace ScribanLanguage.Commands;

public sealed class StdioAction : LspAction
{
    protected override JsonRpc CreateRpc(ParseResult parseResult, IServiceProvider serviceProvider)
    {
        return new(Console.OpenStandardOutput(), Console.OpenStandardInput());
    }
}