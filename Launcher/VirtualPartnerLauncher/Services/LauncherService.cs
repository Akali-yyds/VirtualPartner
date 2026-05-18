using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using VirtualPartnerLauncher.Models;

namespace VirtualPartnerLauncher.Services;

public sealed class LauncherService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string releaseRoot;
    private readonly string configPath;
    private readonly string localRoot;
    private readonly string logsRoot;
    private readonly string settingsPath;
    private readonly HttpClient httpClient = new();
    private readonly List<Process> serviceProcesses = new();
    private readonly object processLock = new();
    private StreamWriter? logWriter;
    private Process? appProcess;
    private CancellationTokenSource? runCts;

    public event Action<string>? LogReceived;

    public string LogsRoot => logsRoot;

    public LauncherService()
    {
        releaseRoot = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        configPath = Path.Combine(releaseRoot, "launcher_config.json");
        localRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VirtualPartner",
            "V1");
        logsRoot = Path.Combine(localRoot, "logs");
        settingsPath = Path.Combine(localRoot, "launcher_settings.json");
    }

    public LauncherSettings LoadSettings(bool defaultValue)
    {
        Directory.CreateDirectory(localRoot);
        if (!File.Exists(settingsPath))
            return new LauncherSettings { ShowDetailedLogs = defaultValue };

        try
        {
            return JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(settingsPath), JsonOptions)
                ?? new LauncherSettings { ShowDetailedLogs = defaultValue };
        }
        catch
        {
            return new LauncherSettings { ShowDetailedLogs = defaultValue };
        }
    }

    public void SaveSettings(LauncherSettings settings)
    {
        Directory.CreateDirectory(localRoot);
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public LauncherConfig LoadConfig()
    {
        if (!File.Exists(configPath))
            throw new FileNotFoundException("launcher_config.json is missing.", configPath);

        var config = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(configPath), JsonOptions);
        if (config == null)
            throw new InvalidOperationException("launcher_config.json is invalid.");

        return config;
    }

    public async Task RunAsync(IProgress<LauncherStatus> status, CancellationToken cancellationToken)
    {
        runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = runCts.Token;
        Directory.CreateDirectory(logsRoot);
        var logPath = Path.Combine(logsRoot, $"launcher-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        logWriter = new StreamWriter(new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = true
        };

        try
        {
            Log($"Release root: {releaseRoot}");
            Log($"Local root: {localRoot}");
            Log($"Log file: {logPath}");

            status.Report(new LauncherStatus("Checking package", 0.05));
            var config = LoadConfig();
            var runtimeDirs = ValidatePackage(config);

            status.Report(new LauncherStatus("Runtime ready", 0.18));
            await StartServicesAsync(config, runtimeDirs, status, token);

            status.Report(new LauncherStatus("Starting game", 0.90));
            await StartAppAsync(config, token);

            status.Report(new LauncherStatus("Running", 1.0));
            if (appProcess != null)
                await appProcess.WaitForExitAsync(token);
            Log("Unity app exited.");
        }
        finally
        {
            await StopAsync();
            logWriter?.Dispose();
            logWriter = null;
        }
    }

    public async Task StopAsync()
    {
        Process? app;
        lock (processLock)
        {
            app = appProcess;
            appProcess = null;
        }

        if (app != null && !app.HasExited)
            KillProcessTree(app.Id);

        List<Process> services;
        lock (processLock)
        {
            services = serviceProcesses.ToList();
            serviceProcesses.Clear();
        }

        foreach (var process in services)
        {
            try
            {
                if (!process.HasExited)
                {
                    Log($"Stopping process tree PID {process.Id}");
                    KillProcessTree(process.Id);
                }
            }
            catch
            {
            }
        }

        runCts?.Cancel();
        await Task.CompletedTask;
    }

    private Dictionary<string, string> ValidatePackage(LauncherConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.RuntimeVersion))
            throw new InvalidOperationException("runtimeVersion is empty.");

        foreach (var relativePath in config.RequiredPaths)
        {
            var path = ResolveReleasePath(relativePath);
            if (!File.Exists(path) && !Directory.Exists(path))
                throw new FileNotFoundException($"Required package path missing: {relativePath}", path);
        }

        var runtimeDirs = ResolveRuntimeDirectories(config);
        foreach (var runtime in config.Runtimes)
        {
            if (!runtimeDirs.TryGetValue(runtime.Id, out var root) || !Directory.Exists(root))
                throw new DirectoryNotFoundException($"{runtime.DisplayName} runtime root missing: {root}");

            if (!string.IsNullOrWhiteSpace(runtime.RequiredExecutable))
            {
                var required = ResolveRuntimeTokens(runtime.RequiredExecutable, runtimeDirs);
                if (!File.Exists(required))
                    throw new FileNotFoundException($"{runtime.DisplayName} required executable missing.", required);
            }

            Log($"Runtime {runtime.Id}: {root}");
        }

        foreach (var service in config.Services)
        {
            ValidateServicePaths(service, runtimeDirs);
            if (service.Port > 0 && !IsLocalPortFree(service.Port))
                throw new InvalidOperationException($"{service.DisplayName} port {service.Port} is already in use.");
        }

        Log($"Package check passed for {config.RuntimeVersion}.");
        return runtimeDirs;
    }

    private Dictionary<string, string> ResolveRuntimeDirectories(LauncherConfig config)
    {
        var runtimeDirs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var runtime in config.Runtimes)
        {
            if (string.IsNullOrWhiteSpace(runtime.Id))
                throw new InvalidOperationException("Runtime id is empty.");
            if (string.IsNullOrWhiteSpace(runtime.RootPath))
                throw new InvalidOperationException($"{runtime.Id} runtime rootPath is empty.");

            runtimeDirs[runtime.Id] = ResolveReleasePath(runtime.RootPath);
        }

        return runtimeDirs;
    }

    private void ValidateServicePaths(ServiceConfig service, Dictionary<string, string> runtimeDirs)
    {
        var executable = ResolveServicePath(service.Executable, runtimeDirs);
        if (!File.Exists(executable))
            throw new FileNotFoundException($"{service.DisplayName} executable missing.", executable);

        var workingDirectory = ResolveServicePath(service.WorkingDirectory, runtimeDirs);
        if (!Directory.Exists(workingDirectory))
            throw new DirectoryNotFoundException($"{service.DisplayName} working directory missing: {workingDirectory}");
    }

    private async Task StartServicesAsync(
        LauncherConfig config,
        Dictionary<string, string> runtimeDirs,
        IProgress<LauncherStatus> status,
        CancellationToken token)
    {
        var gpt = config.Services.FirstOrDefault(service => service.Id.Equals("gpt_sovits", StringComparison.OrdinalIgnoreCase));
        var tts = config.Services.FirstOrDefault(service => service.Id.Equals("tts", StringComparison.OrdinalIgnoreCase));
        var asr = config.Services.FirstOrDefault(service => service.Id.Equals("asr", StringComparison.OrdinalIgnoreCase));

        if (gpt == null || tts == null || asr == null)
        {
            await StartServicesSequentiallyAsync(config, runtimeDirs, status, token);
            return;
        }

        status.Report(new LauncherStatus("Starting GPT-SoVITS and ASR", 0.30));
        var gptTask = StartAndWaitServiceAsync(gpt, runtimeDirs, status, "Starting GPT-SoVITS", 0.35, token);
        var asrTask = StartAndWaitServiceAsync(asr, runtimeDirs, status, "Starting ASR", 0.35, token);

        await gptTask;
        await StartAndWaitServiceAsync(tts, runtimeDirs, status, "Starting TTS", 0.72, token);
        await asrTask;

        status.Report(new LauncherStatus("Services ready", 0.86));
    }

    private async Task StartServicesSequentiallyAsync(
        LauncherConfig config,
        Dictionary<string, string> runtimeDirs,
        IProgress<LauncherStatus> status,
        CancellationToken token)
    {
        for (var i = 0; i < config.Services.Count; i++)
        {
            var service = config.Services[i];
            var progress = 0.30 + (0.55 * i / Math.Max(1, config.Services.Count));
            await StartAndWaitServiceAsync(
                service,
                runtimeDirs,
                status,
                $"Starting {service.DisplayName}",
                progress,
                token);
        }
    }

    private async Task StartAndWaitServiceAsync(
        ServiceConfig service,
        Dictionary<string, string> runtimeDirs,
        IProgress<LauncherStatus> status,
        string statusMessage,
        double progress,
        CancellationToken token)
    {
        status.Report(new LauncherStatus(statusMessage, progress));

        var process = StartService(service, runtimeDirs);
        lock (processLock)
            serviceProcesses.Add(process);

        Log($"Waiting for {service.DisplayName}: {service.HealthUrl}");
        await WaitForHealthAsync(service, process, token);
        Log($"{service.DisplayName} ready.");
    }

    private Process StartService(ServiceConfig service, Dictionary<string, string> runtimeDirs)
    {
        var executable = ResolveServicePath(service.Executable, runtimeDirs);
        var workingDirectory = ResolveServicePath(service.WorkingDirectory, runtimeDirs);

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = service.Arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        ApplyRuntimeEnvironment(startInfo, executable, runtimeDirs);
        startInfo.Environment["PYTHONUTF8"] = "1";
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
        startInfo.Environment["PYTHONUNBUFFERED"] = "1";

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
                Log($"[{service.DisplayName}] {args.Data}");
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
                Log($"[{service.DisplayName}] {args.Data}");
        };

        Log($"Starting {service.DisplayName}: {executable} {service.Arguments}");
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private async Task StartAppAsync(LauncherConfig config, CancellationToken token)
    {
        var appPath = ResolveReleasePath(config.AppPath);
        if (!File.Exists(appPath))
            throw new FileNotFoundException("Unity app executable missing.", appPath);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = appPath,
                WorkingDirectory = Path.GetDirectoryName(appPath) ?? releaseRoot,
                UseShellExecute = false
            },
            EnableRaisingEvents = true
        };

        Log($"Starting Unity app: {appPath}");
        process.Start();
        lock (processLock)
            appProcess = process;
        await Task.Delay(250, token);
    }

    private async Task WaitForHealthAsync(ServiceConfig service, Process process, CancellationToken token)
    {
        var deadline = DateTime.UtcNow.AddSeconds(Math.Max(5, service.StartupTimeoutSeconds));
        var lastError = "waiting";

        while (DateTime.UtcNow < deadline)
        {
            token.ThrowIfCancellationRequested();
            if (process.HasExited)
                throw new InvalidOperationException($"{service.DisplayName} exited early with code {process.ExitCode}. Last health: {lastError}");

            try
            {
                using var response = await httpClient.GetAsync(service.HealthUrl, token);
                var content = await response.Content.ReadAsStringAsync(token);
                if (service.HealthMode.Equals("json-success", StringComparison.OrdinalIgnoreCase))
                {
                    if (response.StatusCode == HttpStatusCode.OK && JsonReportsSuccess(content, out lastError))
                        return;
                }
                else if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(lastError))
                    lastError = $"HTTP {(int)response.StatusCode}";
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex.Message;
            }

            await Task.Delay(1000, token);
        }

        throw new TimeoutException($"{service.DisplayName} health check timed out: {service.HealthUrl}. Last error: {lastError}");
    }

    private static bool JsonReportsSuccess(string content, out string message)
    {
        message = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (root.TryGetProperty("message", out var messageElement))
                message = messageElement.GetString() ?? string.Empty;
            if (root.TryGetProperty("success", out var successElement) && successElement.ValueKind == JsonValueKind.True)
                return true;
            if (string.IsNullOrWhiteSpace(message))
                message = "success=false";
            return false;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    private static void ApplyRuntimeEnvironment(
        ProcessStartInfo startInfo,
        string executable,
        Dictionary<string, string> runtimeDirs)
    {
        var runtimeDir = runtimeDirs.Values
            .Where(dir => executable.StartsWith(Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(dir => dir.Length)
            .FirstOrDefault();

        if (runtimeDir == null)
            return;

        var pathParts = new[]
        {
            runtimeDir,
            Path.Combine(runtimeDir, "Scripts"),
            Path.Combine(runtimeDir, "Library", "bin")
        };
        var currentPath = startInfo.Environment.TryGetValue("PATH", out var path) ? path : string.Empty;
        startInfo.Environment["PATH"] = string.Join(Path.PathSeparator, pathParts.Concat(new[] { currentPath }));
    }

    private string ResolveReleasePath(string value)
    {
        if (Path.IsPathRooted(value))
            return Path.GetFullPath(value);
        return Path.GetFullPath(Path.Combine(releaseRoot, value));
    }

    private string ResolveServicePath(string value, Dictionary<string, string> runtimeDirs)
    {
        var resolved = ResolveRuntimeTokens(value, runtimeDirs);
        if (Path.IsPathRooted(resolved))
            return Path.GetFullPath(resolved);
        return ResolveReleasePath(resolved);
    }

    private static string ResolveRuntimeTokens(string value, Dictionary<string, string> runtimeDirs)
    {
        var resolved = value;
        foreach (var pair in runtimeDirs)
            resolved = resolved.Replace("{runtime:" + pair.Key + "}", pair.Value, StringComparison.OrdinalIgnoreCase);
        return resolved;
    }

    private static bool IsLocalPortFree(int port)
    {
        TcpListener? listener = null;
        try
        {
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            listener?.Stop();
        }
    }

    private static void KillProcessTree(int pid)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "taskkill.exe",
                Arguments = $"/PID {pid} /T /F",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit(5000);
        }
        catch
        {
        }
    }

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        logWriter?.WriteLine(line);
        LogReceived?.Invoke(line);
    }

    public void Dispose()
    {
        httpClient.Dispose();
        logWriter?.Dispose();
        runCts?.Dispose();
    }
}
