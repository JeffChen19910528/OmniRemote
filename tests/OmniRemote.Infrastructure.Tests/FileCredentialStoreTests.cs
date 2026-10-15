using OmniRemote.Infrastructure.Security;

namespace OmniRemote.Infrastructure.Tests;

public sealed class FileCredentialStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"cred_{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task Save_and_load_roundtrip_and_survives_new_instance()
    {
        await new FileCredentialStore(_dir).SaveAsync("rdp:1", "p@ss wörd");

        Assert.Equal("p@ss wörd", await new FileCredentialStore(_dir).LoadAsync("rdp:1"));
    }

    [Fact]
    public async Task Secret_is_not_stored_in_plain_text()
    {
        await new FileCredentialStore(_dir).SaveAsync("ssh:1", "SuperSecret123");

        Assert.DoesNotContain("SuperSecret123", File.ReadAllText(Path.Combine(_dir, "credentials.dat")));
    }

    [Fact]
    public async Task Load_missing_key_returns_null_and_delete_removes()
    {
        var sut = new FileCredentialStore(_dir);
        Assert.Null(await sut.LoadAsync("nope"));

        await sut.SaveAsync("k", "v");
        await sut.DeleteAsync("k");
        Assert.Null(await sut.LoadAsync("k"));
    }

    [Fact]
    public async Task Lost_key_file_yields_null_instead_of_throwing()
    {
        var sut = new FileCredentialStore(_dir);
        await sut.SaveAsync("k", "v");
        File.Delete(Path.Combine(_dir, "credentials.key"));

        Assert.Null(await sut.LoadAsync("k"));
    }
}
