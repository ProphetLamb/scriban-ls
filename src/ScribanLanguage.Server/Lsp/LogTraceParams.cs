using System.Runtime.Serialization;

namespace ScribanLanguage.Lsp;

[DataContract]
public sealed class LogTraceParams
{
    [DataMember(Name = "message")] public string Message { get; set; } = null!;
    [DataMember(Name = "verbose")] public string? Verbose { get; set; }
}