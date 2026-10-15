using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniRemote.App.Services;
using OmniRemote.App.ViewModels;
using OmniRemote.App.Views;
using OmniRemote.Infrastructure;
using OmniRemote.Infrastructure.Security;
using OmniRemote.Protocols.Abstractions;
using OmniRemote.Protocols.Rdp;
using OmniRemote.Protocols.Sftp;
using OmniRemote.Protocols.Ssh;
using OmniRemote.Protocols.Telnet;
using System;
using System.IO;

namespace OmniRemote.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(b => b.AddDebug().SetMinimumLevel(LogLevel.Debug));

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OmniRemote");
        Directory.CreateDirectory(appData);

        AddInfrastructure(services, appData);
        AddProtocols(services);
        AddApp(services);
    }

    // ── Infrastructure: database, credential store, security policies ─────

    private static void AddInfrastructure(IServiceCollection services, string appData)
    {
        services.AddOmniRemoteInfrastructure(Path.Combine(appData, "profiles.db"));

        services.AddSingleton<IHostIdentityPolicy>(sp => new FileBasedHostIdentityPolicy(
            Path.Combine(appData, "known_hosts"),
            sp.GetRequiredService<ILogger<FileBasedHostIdentityPolicy>>()));

        services.AddSingleton<ICertificateValidationPolicy, DefaultCertificateValidationPolicy>();
    }

    // ── Protocol adapters + registry ──────────────────────────────────────

    private static void AddProtocols(IServiceCollection services)
    {
        services.AddSingleton<IRemoteProtocolAdapter, SshAdapter>();
        services.AddSingleton<IRemoteProtocolAdapter, SftpAdapter>();
        services.AddSingleton<IRemoteProtocolAdapter, TelnetAdapter>();
        services.AddSingleton<IRemoteProtocolAdapter, RdpAdapter>();

        // IAdapterRegistry aggregates all registered adapters; ViewModels
        // depend on it rather than the raw adapter list.
        services.AddSingleton<IAdapterRegistry>(sp =>
            new AdapterRegistry(sp.GetServices<IRemoteProtocolAdapter>()));
    }

    // ── Application layer (ViewModels) ────────────────────────────────────

    private static void AddApp(IServiceCollection services)
    {
        services.AddTransient<MainWindowViewModel>(sp => new MainWindowViewModel(
            sp.GetRequiredService<IConnectionProfileStore>(),
            sp.GetRequiredService<IAdapterRegistry>(),
            sp.GetRequiredService<ICredentialStore>()));
    }
}
