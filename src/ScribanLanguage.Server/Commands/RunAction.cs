using System.CommandLine;
using System.CommandLine.Invocation;
using Newtonsoft.Json;
using Scriban;
using Scriban.Parsing;

namespace ScribanLanguage.Commands;

public sealed class RunAction : AsynchronousCommandLineAction
{
    public static Argument<string> Text { get; } = new("text")
    {
        Description = "File path to the Scriban script or template to interpret",
        Validators =
        {
            v =>
            {
                var filePath = v.GetValueOrDefault<string>().NotWhiteSpace();
                if (File.Exists(filePath))
                {
                    return;
                }

                v.AddError($"File at `{v.GetValueOrDefault<string>()}` does not exist");
            }
        }
    };

    public static Option<string> Model { get; } = new("--model")
    {
        Description = "File path to the JSON model accessed when evaluation the script or template",
        Validators =
        {
            v =>
            {
                var filePath = v.GetValueOrDefault<string>().NotWhiteSpace();
                if (filePath is null || File.Exists(filePath))
                {
                    return;
                }

                v.AddError($"File at `{v.GetValueOrDefault<string>()}` does not exist");
            }
        }
    };

    public static Option<ScriptMode> LexerMode { get; } = new("--lexer-mode")
    {
        Description = "Script mode lexer options for parsing the template"
    };

    public static Option<ScriptLang> LexerLang { get; } = new("--lexer-lang")
    {
        Description = "Script lang lexer options for parsing the template",
    };

    public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        var text = await File.ReadAllTextAsync(parseResult.GetRequiredValue(Text), cancellationToken)
            .ConfigureAwait(false);
        LexerOptions options = LexerOptions.Default;
        options.Mode = parseResult.GetValue(LexerMode);
        options.Lang = parseResult.GetValue(LexerLang);
        var template = Template.Parse(text, parseResult.GetRequiredValue(Text), lexerOptions: options);
        var model = parseResult.GetValue(Model).NotWhiteSpace() is { } modelFilePath
            ? JsonConvert.DeserializeObject(await File.ReadAllTextAsync(modelFilePath, cancellationToken)
                .ConfigureAwait(false))
            : null;
        if (options.Mode == ScriptMode.ScriptOnly)
        {
            var result = await template.EvaluateAsync(model, static m => m.Name).ConfigureAwait(false);
            var resultJson = JsonConvert.SerializeObject(result);
            await Console.Out.WriteLineAsync(resultJson.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var result = await template.RenderAsync(model, static m => m.Name).ConfigureAwait(false);
            await Console.Out.WriteLineAsync((result ?? "").AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        return 0;
    }
}