using System.Text.Json.Serialization;

namespace ScalarGui.Models;

public enum SubDirStatus
{
    Pending,
    InProgress,
    Done,
    Failed,
    Blocked,
    Skipped
}

public class SubDirectoryEntry
{
    public string Path { get; set; } = string.Empty;
    public SubDirStatus Status { get; set; } = SubDirStatus.Pending;
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class SparseCheckoutTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string RepoPath { get; set; } = string.Empty;
    public string TargetDirectory { get; set; } = string.Empty;
    public List<SubDirectoryEntry> SubDirectories { get; set; } = [];
    public TaskStatus OverallStatus { get; set; } = TaskStatus.Pending;
    public int MaxRetries { get; set; } = 5;
    public int BaseRetryDelaySeconds { get; set; } = 2;
    public int MaxRetryDelaySeconds { get; set; } = 60;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastResumedAt { get; set; }

    [JsonIgnore]
    public int DoneCount => SubDirectories.Count(d => d.Status == SubDirStatus.Done);

    [JsonIgnore]
    public int FailedCount => SubDirectories.Count(d => d.Status is SubDirStatus.Failed or SubDirStatus.Blocked);

    [JsonIgnore]
    public int PendingCount => SubDirectories.Count(d => d.Status is SubDirStatus.Pending or SubDirStatus.InProgress);

    [JsonIgnore]
    public int TotalCount => SubDirectories.Count;

    [JsonIgnore]
    public double ProgressPercent => TotalCount == 0 ? 0 : (double)DoneCount / TotalCount * 100;
}
