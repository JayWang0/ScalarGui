namespace ScalarGui.Models;

public enum TaskStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

public class CloneOptions
{
    public string? Branch { get; set; }
    public bool SingleBranch { get; set; }
    public bool NoTags { get; set; }
    public bool FullClone { get; set; }
    public bool NoSrc { get; set; }
    public bool NoMaintenance { get; set; }
    public bool UseGvfsProtocol { get; set; }
    public string? CacheServerUrl { get; set; }
}

public class CloneTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string RepoUrl { get; set; } = string.Empty;
    public string TargetDirectory { get; set; } = string.Empty;
    public CloneOptions Options { get; set; } = new();
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public double Progress { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
