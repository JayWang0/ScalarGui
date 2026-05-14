using System.Diagnostics;
using System.Text;

namespace ScalarGui.Services;

public record CommandResult(int ExitCode, string Output, string Error)
{
    public bool Success => ExitCode == 0;
    public bool IsEarlyEof => Error.Contains("early EOF", StringComparison.OrdinalIgnoreCase)
                           || Error.Contains("unexpected disconnect", StringComparison.OrdinalIgnoreCase)
                           || Output.Contains("early EOF", StringComparison.OrdinalIgnoreCase);
}

public class GitCommandService
{
    private string _gitPath = "git";
    private string? _workingDirectory;

    public void Configure(string gitPath, string? workingDirectory = null)
    {
        _gitPath = gitPath;
        _workingDirectory = workingDirectory;
    }

    public void SetWorkingDirectory(string path) => _workingDirectory = path;

    public async Task<CommandResult> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken ct = default,
        int timeoutSeconds = 600)
    {
        return await RunAsync(arguments, null, ct, timeoutSeconds);
    }

    public async Task<CommandResult> RunAsync(
        IReadOnlyList<string> arguments,
        Action<string>? onOutputLine,
        CancellationToken ct = default,
        int timeoutSeconds = 600)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _gitPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        if (_workingDirectory != null)
            psi.WorkingDirectory = _workingDirectory;

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

    // Convenience methods
    public Task<CommandResult> SparseCheckoutListAsync(CancellationToken ct = default)
        => RunAsync(["sparse-checkout", "list"], ct: ct);

    public Task<CommandResult> SparseCheckoutAddAsync(
        string directory, Action<string>? onOutputLine = null, CancellationToken ct = default)
        => RunAsync(["sparse-checkout", "add", directory], onOutputLine, ct: ct, timeoutSeconds: 1800);

    public Task<CommandResult> LsTreeDirectoriesAsync(string path = "", CancellationToken ct = default)
    {
        var args = new List<string> { "ls-tree", "-d", "--name-only", "HEAD" };
        if (!string.IsNullOrEmpty(path))
            args.Add($"{path}/");

        return RunAsync(args, ct: ct);
    }

    public Task<CommandResult> LsTreeAllAsync(string path = "", CancellationToken ct = default)
    {
        var args = new List<string> { "ls-tree", "--name-only", "HEAD" };
        if (!string.IsNullOrEmpty(path))
            args.Add($"{path}/");

        return RunAsync(args, ct: ct);
    }

    public Task<CommandResult> LsTreeRecursiveCountAsync(string path, CancellationToken ct = default)
        => RunAsync(["ls-tree", "-r", "--name-only", "HEAD", path], ct: ct);

    public async Task<CommandResult> ApplyConfigAsync(
        string key,
        string value,
        bool global = false,
        CancellationToken ct = default)
    {
        var args = new List<string> { "config" };
        if (global)
            args.Add("--global");

        args.Add(key);
        args.Add(value);
        return await RunAsync(args, ct: ct);
    }

    public async Task<string?> GetConfigAsync(string key, CancellationToken ct = default)
    {
        var result = await RunAsync(["config", "--get", key], ct: ct);
        return result.Success ? result.Output.Trim() : null;
    }
}
