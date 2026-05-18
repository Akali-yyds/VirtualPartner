using System.Diagnostics;
using System.IO;
using System.Windows;
using VirtualPartnerLauncher.Models;
using VirtualPartnerLauncher.Services;

namespace VirtualPartnerLauncher;

public partial class MainWindow : Window
{
    private readonly LauncherService launcherService = new();
    private readonly CancellationTokenSource windowCts = new();
    private LauncherSettings settings = new();
    private bool isRunning;
    private bool suppressSettingsSave;

    public MainWindow()
    {
        InitializeComponent();
        launcherService.LogReceived += OnLogReceived;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var config = launcherService.LoadConfig();
            settings = launcherService.LoadSettings(config.ShowDetailedLogsDefault);
            suppressSettingsSave = true;
            DetailedLogsCheckBox.IsChecked = settings.ShowDetailedLogs;
            LogsPanel.Visibility = settings.ShowDetailedLogs ? Visibility.Visible : Visibility.Collapsed;
            suppressSettingsSave = false;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            StartButton.IsEnabled = true;
            return;
        }

        await StartLauncherAsync();
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        await StartLauncherAsync();
    }

    private async Task StartLauncherAsync()
    {
        if (isRunning)
            return;

        isRunning = true;
        StartButton.IsEnabled = false;
        ErrorText.Visibility = Visibility.Collapsed;
        ErrorText.Text = string.Empty;
        SetStatus("Checking package", 5);

        var progress = new Progress<LauncherStatus>(status =>
        {
            SetStatus(status.Message, status.Progress * 100);
        });

        try
        {
            await launcherService.RunAsync(progress, windowCts.Token);
            SetStatus("Game exited", 100);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Stopped", 0);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            SetStatus("Launch failed", StartupProgress.Value);
        }
        finally
        {
            isRunning = false;
            StartButton.IsEnabled = true;
        }
    }

    private void DetailedLogsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (suppressSettingsSave)
            return;

        settings.ShowDetailedLogs = DetailedLogsCheckBox.IsChecked == true;
        LogsPanel.Visibility = settings.ShowDetailedLogs ? Visibility.Visible : Visibility.Collapsed;
        launcherService.SaveSettings(settings);
    }

    private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(launcherService.LogsRoot);
        Process.Start(new ProcessStartInfo
        {
            FileName = launcherService.LogsRoot,
            UseShellExecute = true
        });
    }

    private async void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        await launcherService.StopAsync();
        Close();
    }

    private async void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        windowCts.Cancel();
        await launcherService.StopAsync();
        launcherService.Dispose();
    }

    private void SetStatus(string message, double progress)
    {
        StatusText.Text = message;
        StartupProgress.Value = Math.Max(0, Math.Min(100, progress));
        PercentText.Text = $"{StartupProgress.Value:0}%";
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
        AppendLog("[error] " + message);
    }

    private void OnLogReceived(string line)
    {
        Dispatcher.Invoke(() => AppendLog(line));
    }

    private void AppendLog(string line)
    {
        LogTextBox.AppendText(line + Environment.NewLine);
        LogTextBox.ScrollToEnd();
    }
}
