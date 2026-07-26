using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Logger;
using QuickFix.Store;

namespace OEE.MarketAcess;

public class FixGateway : IHostedService, IDisposable
{
    private readonly ILogger<FixGateway> _logger;
    private readonly ThreadedSocketAcceptor _acceptor;

    public FixGateway(FixApplication application, ILogger<FixGateway> logger, IHostEnvironment environment)
    {
        _logger = logger;

#if DEBUG
        _logger.LogInformation("Initialize Fix Gatway in DEBUG mode.");
        string settingsFile = $"fgw.{environment.EnvironmentName.ToLowerInvariant()}.cfg";
        var settings = new SessionSettings(settingsFile);
        var storeFactory = new MemoryStoreFactory();
        var logFactory = new ScreenLogFactory(settings);
        var messageFactory = new DefaultMessageFactory();
        _acceptor = new ThreadedSocketAcceptor(application, storeFactory, settings, logFactory, messageFactory);
#else
       string settingsFile = $"fgw.{environment.EnvironmentName.ToLowerInvariant()}.cfg";
        var settings = new SessionSettings(settingsFile);
        var storeFactory = new FileStoreFactory(settings);
        var logFactory = new FileLogFactory(settings);
        var messageFactory = new DefaultMessageFactory();
        _acceptor = new ThreadedSocketAcceptor(application, storeFactory, settings, logFactory, messageFactory);
#endif
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _acceptor.Start();

        _logger.LogInformation("Fix Gateway started.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _acceptor.Stop();

        _logger.LogInformation("Fix Gateway stopped.");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _acceptor.Dispose();
    }
}
