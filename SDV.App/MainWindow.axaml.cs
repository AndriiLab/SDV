using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using SDV.App.Logging;
using Serilog.Events;
using SDV.GraphGenerator.Interfaces;

namespace SDV.App
{
    /// <summary>
    /// Interaction logic for MainWindow.axaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly FilePickerOpenOptions SlnPickerOptions = new()
        {
            Title = "Select Sln file",
            AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType("Solution files") { Patterns = ["*.sln"] }]
        };

        private readonly ILogger<MainWindow> _logger;
        private readonly IGraphBuilder _builder;
        private HashSet<string> _slnFilePaths;

        public MainWindow(ILogger<MainWindow> logger, WindowLogSink logSink, IGraphBuilder builder)
        {
            _logger = logger;
            _builder = builder;
            InitializeComponent();
            _slnFilePaths = new HashSet<string>();
            PackageFiltersExclude.Text = "Microsoft.*, System.*";
            IncludeDependentProjects.IsChecked = true;
            ClearSelectionButton.IsEnabled = false;
            Labels.Text = "IsNuget=📦";
            logSink.OnLogEmitted = OnLogEmitted;
        }

        private async void btnOpenFile_Click(object? sender, RoutedEventArgs e)
        {
            ToggleControlsAs(false);

            try
            {
                var files = await StorageProvider.OpenFilePickerAsync(SlnPickerOptions);
                foreach (var path in files.Select(f => f.TryGetLocalPath()).OfType<string>())
                {
                    _logger.LogInformation("Selected {File}", path);
                    _slnFilePaths.Add(path);
                    ClearSelectionButton.IsEnabled = true;
                }

                if (_slnFilePaths.Count > 1)
                {
                    _logger.LogInformation("Total solutions selected: {Number}", _slnFilePaths.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to select sln file");
            }
            finally
            {
                ToggleControlsAs(true);
            }
        }

        private async void btnBuild_Click(object? sender, RoutedEventArgs e)
        {
            if (_slnFilePaths.Count < 1 || !_slnFilePaths.All(p => p.EndsWith(".sln")))
            {
                _logger.LogError("Please, select sln file to proceed");
                return;
            }

            ToggleControlsAs(false);

            try
            {
                var request = new GraphBuilderRequest(_slnFilePaths)
                {
                    FiltersInclude = GetCommaSeparatedValues(PackageFiltersInclude.Text).ToArray(),
                    FiltersExclude = GetCommaSeparatedValues(PackageFiltersExclude.Text).ToArray(),
                    Labels = GetCommaSeparatedValues(Labels.Text).Select(s => s.Split('=')).Where(a => a.Length == 2)
                        .GroupBy(s => s[0].Trim())
                        .ToDictionary(
                            g => g.Key,
                            g => g.SelectMany(v => v).Select(s => s.Trim())
                                .Where(s => !string.IsNullOrEmpty(s) && s != g.Key).ToArray()),
                    IncludeDependentProjects = IncludeDependentProjects.IsChecked ?? false,
                    MergeProjects = MergeProjects.IsChecked ?? false
                };

                var path = await BuildGraphAndGetFilePathAsync(request);

                await OpenFileAsync(path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occured upon generation of graph");
                _logger.LogError("{StackTrace}", ex.StackTrace);
            }
            finally
            {
                ToggleControlsAs(true);
            }
        }

        private static IEnumerable<string> GetCommaSeparatedValues(string? str)
        {
            return (str ?? string.Empty).Split(",").Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t));
        }

        private Task<string> BuildGraphAndGetFilePathAsync(GraphBuilderRequest graphBuilderRequest) =>
            Task.Run(() => _builder.BuildGraphAndGetFilePath(graphBuilderRequest));

        private void btnClearSelection_Click(object? sender, RoutedEventArgs e)
        {
            _slnFilePaths = [];
            Log.Text = string.Empty;
            _logger.LogInformation("Selection cleared");
            ClearSelectionButton.IsEnabled = false;
        }

        private async Task OpenFileAsync(string path)
        {
            if (!await Launcher.LaunchFileInfoAsync(new FileInfo(path)))
            {
                _logger.LogWarning("Unable to open {File} automatically, please open it manually", path);
            }
        }

        private void ToggleControlsAs(bool state)
        {
            SlnFileSelectorButton.IsEnabled = state;
            PackageFiltersInclude.IsEnabled = state;
            PackageFiltersExclude.IsEnabled = state;
            Labels.IsEnabled = state;
            IncludeDependentProjects.IsEnabled = state;
            MergeProjects.IsEnabled = state;
            BuildGraphButton.IsEnabled = state;
        }

        private void OnLogEmitted(LogEventLevel level, string log)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Log.Foreground = GetBrushForLevel(level);
                Log.Text += log;
                Log.CaretIndex = Log.Text.Length;
            }, DispatcherPriority.Background);
        }

        private static IBrush GetBrushForLevel(LogEventLevel level)
        {
            return level switch
            {
                LogEventLevel.Verbose => Brushes.Green,
                LogEventLevel.Debug => Brushes.Teal,
                LogEventLevel.Information => Brushes.Black,
                LogEventLevel.Warning => Brushes.DarkOrange,
                LogEventLevel.Error => Brushes.DarkRed,
                LogEventLevel.Fatal => Brushes.DarkViolet,
                _ => Brushes.Black
            };
        }
    }
}
