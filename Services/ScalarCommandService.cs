using System.Diagnostics;
using System.Text;

namespace ScalarGui.Services;

public class ScalarCommandService
{
    private string _scalarPath = "scalar";

    public void Configure(string scalarPath)
    {
        _scalarPath = scalarPath;
    }

    public async Task<CommandResult> CloneAsync(
        string repoUrl,
        string targetDirectory,
        Models.CloneOptions options,
        Action<string>? onOutputLine = null,
        CancellationToken ct = default)
    {
        var args = new List<string> { "clone" };

        if (!string.IsNullOrEmpty(options.Branch))
        {
            args.Add("--branch");
            args.Add(options.Branch);
        }
        if (options.SingleBranch)
            args.Add("--single-branch");
        if (options.NoTags)
            args.Add("--no-tags");
        if (options.FullClone)
            args.Add("--full-clone");
        if (options.NoSrc)
            args.Add("--no-src");
        if (options.NoMaintenance)
            args.Add("--no-maintenance");
        if (options.UseGvfsProtocol)
            args.Add("--gvfs-protocol");
        if (!string.IsNullOrEmpty(options.CacheServerUrl))
        {
            args.Add("--cache-server-url");
            args.Add(options.CacheServerUrl);
        }

        args.Add(repoUrl);
        args.Add(targetDirectory);

        return await RunAsync(args, null, onOutputLine, ct, timeoutSeconds: 7200);
    }

    public async Task<List<string>> ListAsync(CancellationToken ct = default)
    {
        var result = await RunAsync(["list"], ct: ct);
        if (!result.Success) return [];

        return result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    public Task<CommandResult> VersionAsync(CancellationToken ct = default)
        => RunAsync(["version"], ct: ct);

    public Task<CommandResult> RegisterAsync(string path, CancellationToken ct = default)
        => RunAsync(["register", path], ct: ct);

    public Task<CommandResult> UnregisterAsync(string path, CancellationToken ct = default)
        => RunAsync(["unregister", path], ct: ct);

    public Task<CommandResult> RunMaintenanceAsync(string task, string? enlistment = null, CancellationToken ct = default)
    {
        var args = new List<string> { "run", task };
        if (enlistment != null)
            args.Add(enlistment);
        return RunAsync(args, ct: ct);
    }

    private async Task<CommandResult> RunAsync(
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        Action<string>? onOutputLine = null,
        CancellationToken ct = default,
        int timeoutSeconds = 600)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _scalarPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        if (workingDirectory != null)
            psi.WorkingDirectory = workingDirectory;

        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            stdout.AppendLine(e.Data);
            onOutputLine?.Invoke(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            stderr.AppendLine(e.Data);
            onOutputLine?.Invoke(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            throw;
        }

        return new CommandResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
