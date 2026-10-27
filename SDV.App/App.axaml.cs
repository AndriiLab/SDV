using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SDV.App.Logging;
using Serilog;
using SDV.DependenciesAnalyzer;
using SDV.DependenciesAnalyzer.Interfaces;
using SDV.GraphGenerator.Interfaces;
using SDV.GraphGenerator.Services;

namespace SDV.App
{
    public partial class App : Application
    {
        private readonly IHost _appHost;
        private readonly WindowLogSink _logEventSink;

        public App()
        {
            _logEventSink = new WindowLogSink();
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Sink(_logEventSink)
                .CreateLogger();

            _appHost = Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                .Build();
        }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            _appHost.Start();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = _appHost.Services.GetRequiredService<MainWindow>();
                desktop.Exit += (_, _) =>
                {
                    Log.Debug("Exiting application");
                    _appHost.StopAsync().GetAwaiter().GetResult();
                    Log.CloseAndFlush();
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void ConfigureServices(HostBuilderContext hostBuilderContext,
            IServiceCollection serviceCollection)
        {
            serviceCollection.AddLogging(b => b.AddSerilog(dispose: true));
            serviceCollection.AddSingleton<MainWindow>();
            serviceCollection.AddSingleton(_logEventSink);
            serviceCollection.AddSingleton<IGraphBuilder, GraphBuilder>();
            serviceCollection.AddSingleton<INugetDependenciesGenerator, NugetDependenciesGenerator>();
            serviceCollection.AddSingleton<IGraphDataGenerator, GraphDataGenerator>();
        }
    }
}
