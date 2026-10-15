using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OmniRemote.Core.Models;
using OmniRemote.Protocols.Abstractions;
using System.Text.Json;

namespace OmniRemote.Infrastructure.Persistence;

public sealed class SqliteConnectionProfileStore : IConnectionProfileStore, IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ILogger<SqliteConnectionProfileStore> _logger;

    public SqliteConnectionProfileStore(string dbPath, ILogger<SqliteConnectionProfileStore> logger)
    {
        _logger = logger;
        _connection = new SqliteConnection($"Data Source={dbPath}");
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _connection.OpenAsync(cancellationToken);
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS ConnectionProfiles (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Protocol TEXT NOT NULL,
                JsonData TEXT NOT NULL
            );
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Connection profile store initialized at {Path}", _connection.DataSource);
    }

    public async Task<IReadOnlyList<ConnectionProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<ConnectionProfile>();
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Protocol, JsonData FROM ConnectionProfiles ORDER BY Name;";
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var profile = Deserialize(reader.GetString(0), reader.GetString(1));
            if (profile is not null) results.Add(profile);
        }
        return results;
    }

    public async Task<ConnectionProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Protocol, JsonData FROM ConnectionProfiles WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id.ToString());
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return Deserialize(reader.GetString(0), reader.GetString(1));
    }

    public async Task SaveAsync(ConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ConnectionProfiles (Id, Name, Protocol, JsonData)
            VALUES ($id, $name, $protocol, $json)
            ON CONFLICT(Id) DO UPDATE SET Name=$name, Protocol=$protocol, JsonData=$json;
            """;
        cmd.Parameters.AddWithValue("$id", profile.Id.ToString());
        cmd.Parameters.AddWithValue("$name", profile.Name);
        cmd.Parameters.AddWithValue("$protocol", profile.Protocol.ToString());
        cmd.Parameters.AddWithValue("$json", Serialize(profile));
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM ConnectionProfiles WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id.ToString());
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Serialize(ConnectionProfile profile) =>
        JsonSerializer.Serialize(profile, profile.GetType());

    private static ConnectionProfile? Deserialize(string protocol, string json)
    {
        return protocol switch
        {
            nameof(ProtocolType.Ssh) => JsonSerializer.Deserialize<SshConnectionProfile>(json),
            nameof(ProtocolType.Sftp) => JsonSerializer.Deserialize<SftpConnectionProfile>(json),
            nameof(ProtocolType.Telnet) => JsonSerializer.Deserialize<TelnetConnectionProfile>(json),
            nameof(ProtocolType.Rdp) => JsonSerializer.Deserialize<RdpConnectionProfile>(json),
            _ => null,
        };
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
