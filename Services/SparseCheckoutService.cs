using ScalarGui.Models;
using System.Diagnostics;
using System.IO;

namespace ScalarGui.Services;

/// <summary>
/// Core engine: incrementally adds subdirectories to sparse-checkout one by one,
/// with exponential-backoff retry and resume-from-checkpoint support.
/// </summary>
public class SparseCheckoutService(
    GitCommandService git,
    TreeBrowserService treeBrowser,
    TaskPersistenceService persistence)
{
    public event Action<string>? LogMessage;
    public event Action<SparseCheckoutTask>? ProgressChanged;

    /// <summary>
    /// Create a new incremental sparse-checkout task for the target directory.
    /// Recursively discovers ALL subdirectories and orders them deepest-first (bottom-up),
    /// so leaf directories are added before parents, minimizing blob download per step.
    /// </summary>
    public async Task<SparseCheckoutTask> CreateTaskAsync(
        string repoPath, string targetDirectory, CancellationToken ct = default)
    {
        Log($"Recursively discovering subdirectories of '{targetDirectory}'...");

        int discovered = 0;
        var allSubdirs = await treeBrowser.GetAllDirectoriesRecursiveAsync(
            targetDirectory, ct,
            dir =>
            {
                discovered++;
                if (discovered % 50 == 0)
                    Log($"  ...discovered {discovered} subdirectories so far");
            });

        // Build entries: deepest directories first, target directory last
        var entries = allSubdirs
            .Select(d => new SubDirectoryEntry { Path = d })
            .ToList();

        // Always add the target directory itself at the very end
        entries.Add(new SubDirectoryEntry { Path = targetDirectory });

        var settings = await persistence.LoadSettingsAsync();
        var task = new SparseCheckoutTask
        {
            RepoPath = repoPath,
            TargetDirectory = targetDirectory,
            SubDirectories = entries,
            MaxRetries = settings.MaxRetries,
            BaseRetryDelaySeconds = settings.BaseRetryDelaySeconds,
            MaxRetryDelaySeconds = settings.MaxRetryDelaySeconds
        };

        Log($"Found {allSubdirs.Count} subdirectories. Will process {task.TotalCount} entries (deepest-first, target directory last).");
        await persistence.SaveTaskAsync(task);
        return task;
    }

    /// <summary>
    /// Execute (or resume) an incremental sparse-checkout task.
    /// </summary>
    public async Task ExecuteAsync(
        SparseCheckoutTask task,
        CancellationToken ct = default)
    {
        task.OverallStatus = Models.TaskStatus.Running;
        task.LastResumedAt = DateTime.Now;
        ProgressChanged?.Invoke(task);

        foreach (var entry in task.SubDirectories)
        {
            ct.ThrowIfCancellationRequested();

            if (entry.Status == SubDirStatus.Done || entry.Status == SubDirStatus.Skipped)
                continue;

            entry.Status = SubDirStatus.InProgress;
            ProgressChanged?.Invoke(task);

            var success = await TryAddWithRetryAsync(task, entry, ct);

            if (success)
            {
                entry.Status = SubDirStatus.Done;
                entry.CompletedAt = DateTime.Now;
                Log($"✅ [{task.DoneCount}/{task.TotalCount}] {entry.Path}");
            }
            else
            {
                entry.Status = entry.RetryCount >= task.MaxRetries
                    ? SubDirStatus.Blocked
                    : SubDirStatus.Failed;
                Log($"❌ [{task.DoneCount}/{task.TotalCount}] {entry.Path} — {entry.LastError}");
            }

            // Persist after every subdirectory for crash recovery
            await persistence.SaveTaskAsync(task);
            ProgressChanged?.Invoke(task);
        }

        task.OverallStatus = task.FailedCount == 0
            ? Models.TaskStatus.Completed
            : Models.TaskStatus.Failed;

        await persistence.SaveTaskAsync(task);
        ProgressChanged?.Invoke(task);

        Log($"Finished: {task.DoneCount} done, {task.FailedCount} failed, "
          + $"{task.PendingCount} pending out of {task.TotalCount} total.");
    }

    /// <summary>
    /// Retry only the failed/blocked entries in an existing task.
    /// </summary>
    public async Task RetryFailedAsync(SparseCheckoutTask task, CancellationToken ct = default)
    {
        foreach (var entry in task.SubDirectories
                     .Where(e => e.Status is SubDirStatus.Failed or SubDirStatus.Blocked))
        {
            entry.Status = SubDirStatus.Pending;
            entry.RetryCount = 0;
        }

        await ExecuteAsync(task, ct);
    }

    private async Task<bool> TryAddWithRetryAsync(
        SparseCheckoutTask task, SubDirectoryEntry entry, CancellationToken ct)
    {
        for (int attempt = 0; attempt <= task.MaxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            // Clean up stale lock files before each attempt
            CleanupLockFiles(task.RepoPath);

            if (attempt > 0)
            {
                var delay = CalculateBackoff(attempt, task.BaseRetryDelaySeconds, task.MaxRetryDelaySeconds);
                Log($"⏳ Retry {attempt}/{task.MaxRetries} for '{entry.Path}' in {delay}s...");
                await Task.Delay(TimeSpan.FromSeconds(delay), ct);
            }

            entry.RetryCount = attempt;
            Log($"📥 Adding '{entry.Path}' to sparse-checkout (attempt {attempt + 1})...");

            var sw = Stopwatch.StartNew();
            var result = await git.SparseCheckoutAddAsync(
                entry.Path,
                line => Log($"  git: {line}"),
                ct);
            sw.Stop();

            Log($"  ⏱️ Elapsed: {sw.Elapsed.TotalSeconds:F1}s | ExitCode: {result.ExitCode}");

            if (!result.Success)
            {
                // Command failed — clean up lock files for next attempt
                CleanupLockFiles(task.RepoPath);

                entry.LastError = result.IsEarlyEof
                    ? $"early EOF (attempt {attempt + 1})"
                    : result.Error.Trim().Split('\n').LastOrDefault() ?? "Unknown error";
                Log($"⚠️ Command failed: {entry.LastError}");
                await persistence.SaveTaskAsync(task);
                continue;
            }

            // Exit code 0 — verify files actually materialized on disk
            var dirOnDisk = Path.Combine(task.RepoPath, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(dirOnDisk))
            {
                entry.LastError = $"Directory not found on disk after add (attempt {attempt + 1})";
                Log($"⚠️ Verification failed: {entry.LastError}");
                CleanupLockFiles(task.RepoPath);
                await persistence.SaveTaskAsync(task);
                continue;
            }

            // Check that there's at least one file or subdirectory
            bool hasContent = Directory.EnumerateFileSystemEntries(dirOnDisk).Any();
            if (!hasContent)
            {
                entry.LastError = $"Directory exists but is empty (attempt {attempt + 1})";
                Log($"⚠️ Verification failed: {entry.LastError}");
                CleanupLockFiles(task.RepoPath);
                await persistence.SaveTaskAsync(task);
                continue;
            }

            Log($"  ✔ Verified: '{entry.Path}' exists on disk with content");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Delete stale git lock files that block subsequent operations.
    /// </summary>
    private void CleanupLockFiles(string repoPath)
    {
        string[] lockFiles =
        [
            Path.Combine(repoPath, ".git", "index.lock"),
            Path.Combine(repoPath, ".git", "info", "sparse-checkout.lock"),
        ];

        foreach (var lockFile in lockFiles)
        {
            try
            {
                if (File.Exists(lockFile))
                {
                    File.Delete(lockFile);
                    Log($"🔓 Deleted lock file: {Path.GetFileName(lockFile)}");
                }
            }
            catch (Exception ex)
            {
                Log($"⚠️ Could not delete lock file {Path.GetFileName(lockFile)}: {ex.Message}");
            }
        }
    }

    private static int CalculateBackoff(int attempt, int baseDelay, int maxDelay)
    {
        var delay = baseDelay * (int)Math.Pow(2, attempt - 1);
        return Math.Min(delay, maxDelay);
    }

    private void Log(string message)
    {
        LogMessage?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
    }
}
