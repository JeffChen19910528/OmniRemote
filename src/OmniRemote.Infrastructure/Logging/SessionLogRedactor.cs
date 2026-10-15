using System.Text.RegularExpressions;
using OmniRemote.Protocols.Abstractions;

namespace OmniRemote.Infrastructure.Logging;

public sealed partial class SessionLogRedactor : ISessionLogRedactor
{
    private static readonly string Replacement = "[REDACTED]";

    public string Redact(string message)
    {
        message = PasswordPattern().Replace(message, $"$1{Replacement}");
        message = TokenPattern().Replace(message, $"$1{Replacement}");
        return message;
    }

    [GeneratedRegex(@"(password[=:\s]+)\S+", RegexOptions.IgnoreCase)]
    private static partial Regex PasswordPattern();

    [GeneratedRegex(@"(token[=:\s]+)\S+", RegexOptions.IgnoreCase)]
    private static partial Regex TokenPattern();
}
