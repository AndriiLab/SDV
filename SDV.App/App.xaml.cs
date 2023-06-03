using System.Windows;
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
    public partial class App
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

        protected override async void OnStartup(StartupEventArgs e)
        {
            await _appHost.StartAsync();

            var mainWindow = _appHost.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            Log.Debug("Exiting application");
            await _appHost.StopAsync();
            await Log.CloseAndFlushAsync();
            base.OnExit(e);
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