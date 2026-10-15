using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace OmniRemote.App.Tests;

/// <summary>
/// Static hardening checks. These tests are intentionally broad — they scan
/// source files for patterns that should never appear in committed code.
/// </summary>
public sealed class HardeningTests
{
    // ── Secret scan ───────────────────────────────────────────────────────

    // Patterns indicating a hardcoded credential in source code.
    private static readonly Regex[] SecretPatterns =
    [
        new(@"password\s*=\s*""[^""\s]{4,}""", RegexOptions.IgnoreCase),
        new(@"passwd\s*=\s*""[^""\s]{4,}""",   RegexOptions.IgnoreCase),
        new(@"secret\s*=\s*""[^""\s]{4,}""",   RegexOptions.IgnoreCase),
        new(@"api_?key\s*=\s*""[^""\s]{4,}""", RegexOptions.IgnoreCase),
        new(@"private_?key\s*=\s*""[^""\s]{8,}""", RegexOptions.IgnoreCase),
        // AWS / common token patterns
        new(@"AKIA[0-9A-Z]{16}"),
        new(@"-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----"),
    ];

    [Fact]
    public void Source_files_contain_no_hardcoded_secrets()
    {
        var srcRoot = FindSourceRoot();
        var csFiles = Directory.GetFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .ToList();

        Assert.NotEmpty(csFiles); // Sanity: we must have found source files

        var violations = new List<string>();

        foreach (var file in csFiles)
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                // Skip test doubles that intentionally use literal strings for test data
                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;

                foreach (var pattern in SecretPatterns)
                {
                    if (pattern.IsMatch(line))
                        violations.Add($"{Path.GetRelativePath(srcRoot, file)}:{i + 1}: {line.Trim()}");
                }
            }
        }

        Assert.True(violations.Count == 0,
            $"Potential hardcoded secrets detected in source files:\n{string.Join("\n", violations)}");
    }

    // ── Security invariant checks ─────────────────────────────────────────

    [Fact]
    public void BypassCertificateValidation_defaults_to_false()
    {
        // Ensure the base model's safe default is never changed.
        var profile = new OmniRemote.Core.Models.TelnetConnectionProfile
        {
            Name = "x", Host = "x",
        };
        Assert.False(profile.BypassCertificateValidation);
    }

    [Fact]
    public void SshConnectionProfile_StrictHostKeyChecking_defaults_to_true()
    {
        var profile = new OmniRemote.Core.Models.SshConnectionProfile
        {
            Name = "x", Host = "x", Username = "u",
        };
        Assert.True(profile.StrictHostKeyChecking);
    }

    [Fact]
    public void SftpConnectionProfile_StrictHostKeyChecking_defaults_to_true()
    {
        var profile = new OmniRemote.Core.Models.SftpConnectionProfile
        {
            Name = "x", Host = "x", Username = "u",
        };
        Assert.True(profile.StrictHostKeyChecking);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string FindSourceRoot()
    {
        // Walk up from the test assembly until we find the solution root (contains OmniRemote.slnx).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (dir.GetFiles("*.slnx").Length > 0 || dir.GetFiles("*.sln").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate solution root from test directory.");
    }
}
