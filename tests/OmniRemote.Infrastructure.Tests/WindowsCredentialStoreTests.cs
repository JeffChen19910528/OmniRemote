using OmniRemote.Infrastructure.Security;
using System.Runtime.InteropServices;

namespace OmniRemote.Infrastructure.Tests;

/// <summary>
/// Integration tests for WindowsCredentialStore.
/// These tests require Windows Credential Manager and are BLOCKED on non-Windows platforms.
/// </summary>
public sealed class WindowsCredentialStoreTests
{
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private readonly WindowsCredentialStore _sut = new();
    private readonly string _testKey = $"OmniRemoteTest:{Guid.NewGuid():N}";

    [Fact]
    public async Task Save_and_load_roundtrip()
    {
        if (!IsWindows) return; // BLOCKED: Windows Credential Manager unavailable

        await _sut.SaveAsync(_testKey, "TestSecret!");
        try
        {
            var loaded = await _sut.LoadAsync(_testKey);
            Assert.Equal("TestSecret!", loaded);
        }
        finally
        {
            await _sut.DeleteAsync(_testKey);
        }
    }

    [Fact]
    public async Task Load_nonexistent_key_returns_null()
    {
        if (!IsWindows) return; // BLOCKED

        var result = await _sut.LoadAsync($"OmniRemoteTest_nonexistent_{Guid.NewGuid():N}");
        Assert.Null(result);
    }

    [Fact]
    public async Task Delete_nonexistent_key_does_not_throw()
    {
        if (!IsWindows) return; // BLOCKED

        await _sut.DeleteAsync($"OmniRemoteTest_nonexistent_{Guid.NewGuid():N}");
    }

    [Fact]
    public async Task Overwrite_existing_key()
    {
        if (!IsWindows) return; // BLOCKED

        await _sut.SaveAsync(_testKey, "First");
        await _sut.SaveAsync(_testKey, "Second");
        try
        {
            var loaded = await _sut.LoadAsync(_testKey);
            Assert.Equal("Second", loaded);
        }
        finally
        {
            await _sut.DeleteAsync(_testKey);
        }
    }

    [Fact]
    public async Task Delete_removes_secret()
    {
        if (!IsWindows) return; // BLOCKED

        await _sut.SaveAsync(_testKey, "ToDelete");
        await _sut.DeleteAsync(_testKey);
        var loaded = await _sut.LoadAsync(_testKey);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task Non_windows_load_throws_platform_not_supported()
    {
        if (IsWindows) return; // this test is only meaningful on non-Windows

        await Assert.ThrowsAsync<PlatformNotSupportedException>(
            () => _sut.LoadAsync("anything"));
    }
}
