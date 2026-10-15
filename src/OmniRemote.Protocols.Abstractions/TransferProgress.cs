namespace OmniRemote.Protocols.Abstractions;

public sealed record TransferProgress(
    string FileName,
    long BytesTransferred,
    long TotalBytes)
{
    public double Fraction => TotalBytes > 0 ? (double)BytesTransferred / TotalBytes : 0;
}
