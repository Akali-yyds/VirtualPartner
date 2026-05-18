namespace VirtualPartnerLauncher.Services;

public sealed class LauncherStatus
{
    public string Message { get; }
    public double Progress { get; }

    public LauncherStatus(string message, double progress)
    {
        Message = message;
        Progress = progress;
    }
}
