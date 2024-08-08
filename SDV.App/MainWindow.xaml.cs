using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SDV.App.Logging;
using Serilog.Events;
using SDV.GraphGenerator.Interfaces;

namespace SDV.App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly ILogger<MainWindow> _logger;
        private readonly IGraphBuilder _builder;
        private readonly OpenFileDialog _openFileDialog;
        private HashSet<string> _slnFilePaths;

        public MainWindow(ILogger<MainWindow> logger, WindowLogSink logSink, IGraphBuilder builder)
        {
            _logger = logger;
            _builder = builder;
            InitializeComponent();
            _slnFilePaths = new HashSet<string>();
            _openFileDialog = new OpenFileDialog
            {
                Filter = "solution files (*.sln)|*.sln"
            };
            PackageFiltersExclude.Text = "Microsoft.*, System.*";
            IncludeDependentProjects.IsChecked = true;
            ClearSelectionButton.IsEnabled = false;
            Labels.Text = "IsNuget=\ud83d\udce6";
            logSink.OnLogEmitted = OnLogEmitted;
        }

        private void btnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            ToggleControlsAs(false);

            if (_openFileDialog.ShowDialog() == true)
            {
                _logger.LogInformation("Selected {File}", _openFileDialog.FileName);
                _slnFilePaths.Add(_openFileDialog.FileName);
                ClearSelectionButton.IsEnabled = true;
            }

            if (_slnFilePaths.Count > 1)
            {
                _logger.LogInformation("Total solutions selected: {Number}", _slnFilePaths.Count);
            }

            ToggleControlsAs(true);
        }

        private async void btnBuild_Click(object sender, RoutedEventArgs e)
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

                OpenUrl(path);
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

        private static IEnumerable<string> GetCommaSeparatedValues(string str)
        {
            return str.Split(",").Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t));
        }

        private Task<string> BuildGraphAndGetFilePathAsync(GraphBuilderRequest graphBuilderRequest) =>
            Task.Run(() => _builder.BuildGraphAndGetFilePath(graphBuilderRequest));

        private void btnClearSelection_Click(object sender, RoutedEventArgs e)
        {
            _slnFilePaths = [];
            Log.Text = string.Empty;
            _logger.LogInformation("Selection cleared");
            ClearSelectionButton.IsEnabled = false;
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
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
            Log.Dispatcher.Invoke(DispatcherPriority.Background, () =>
            {
                Log.Foreground = GetBrushForLevel(level);
                Log.AppendText(log);
                Log.ScrollToEnd();
            });
        }

        private static SolidColorBrush GetBrushForLevel(LogEventLevel level)
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