using System.Runtime.Serialization;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace ScribanLanguage.Lsp;

[DataContract]
public sealed class SetTraceParams
{
    [DataMember(Name = "value", IsRequired = true)]
    public TraceSetting Value { get; set; }
}