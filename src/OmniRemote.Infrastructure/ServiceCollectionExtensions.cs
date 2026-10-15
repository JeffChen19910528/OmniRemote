using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniRemote.Core.Validation;
using OmniRemote.Infrastructure.Export;
using OmniRemote.Infrastructure.Logging;
using OmniRemote.Infrastructure.Persistence;
using OmniRemote.Infrastructure.Security;
using OmniRemote.Protocols.Abstractions;

namespace OmniRemote.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOmniRemoteInfrastructure(this IServiceCollection services, string dbPath)
    {
        services.AddSingleton<ISessionLogRedactor, SessionLogRedactor>();
        services.AddSingleton<ICredentialStore, WindowsCredentialStore>();
        services.AddSingleton<IConnectionProfileStore>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SqliteConnectionProfileStore>>();
            var store = new SqliteConnectionProfileStore(dbPath, logger);
            store.InitializeAsync().GetAwaiter().GetResult();
            return store;
        });
        services.AddSingleton<ProfileValidator>();
        services.AddSingleton<ProfileExporter>();
        return services;
    }
}
