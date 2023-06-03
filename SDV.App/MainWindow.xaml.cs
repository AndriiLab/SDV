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
using SDV.DependenciesAnalyzer.Interfaces;
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
            PackagePrefixes.Text = "Microsoft., System.";
            IncludeDependentProjects.IsChecked = true;
            ClearSelectionButton.IsEnabled = false;
            SetupModeComboBox();
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
                    Mode = (PackageFilterMode)Mode.SelectedValue,
                    PackagePrefixes = PackagePrefixes.Text.Split(",").Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToArray(),
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

        private Task<string> BuildGraphAndGetFilePathAsync(GraphBuilderRequest graphBuilderRequest) => 
            Task.Run(() => _builder.BuildGraphAndGetFilePath(graphBuilderRequest));

        private void btnClearSelection_Click(object sender, RoutedEventArgs e)
        {
            _slnFilePaths = new HashSet<string>();
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

        private void SetupModeComboBox()
        {
            var item = Tuple.Create("Disabled", PackageFilterMode.None);
            Mode.DisplayMemberPath = nameof(item.Item1);
            Mode.SelectedValuePath = nameof(item.Item2);
            Mode.SelectedValue = PackageFilterMode.Exclude;
            Mode.Items.Add(item);
            Mode.Items.Add(Tuple.Create("Include listed packages", PackageFilterMode.Include));
            Mode.Items.Add(Tuple.Create("Exclude listed packages", PackageFilterMode.Exclude));
        }

        private void ToggleControlsAs(bool state)
        {
            SlnFileSelectorButton.IsEnabled = state;
            Mode.IsEnabled = state;
            PackagePrefixes.IsEnabled = state;
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

        private static Brush GetBrushForLevel(LogEventLevel level)
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