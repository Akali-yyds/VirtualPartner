using System.Collections.Generic;

namespace VirtualPartnerLauncher.Models;

public sealed class LauncherConfig
{
    public string RuntimeVersion { get; set; } = "v1";
    public string AppPath { get; set; } = "App\\VirtualPartner.exe";
    public bool ShowDetailedLogsDefault { get; set; }
    public List<string> RequiredPaths { get; set; } = new();
    public List<RuntimeConfig> Runtimes { get; set; } = new();
    public List<ServiceConfig> Services { get; set; } = new();
}

public sealed class RuntimeConfig
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public string RequiredExecutable { get; set; } = string.Empty;
}

public sealed class ServiceConfig
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string Executable { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public int Port { get; set; }
    public string HealthUrl { get; set; } = string.Empty;
    public string HealthMode { get; set; } = "http-ok";
    public int StartupTimeoutSeconds { get; set; } = 120;
}
