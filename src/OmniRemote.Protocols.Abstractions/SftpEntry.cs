namespace OmniRemote.Protocols.Abstractions;

public sealed record SftpEntry(
    string Name,
    string FullPath,
    bool IsDirectory,
    bool IsSymbolicLink,
    long SizeBytes,
    DateTimeOffset LastModified,
    string Permissions);
