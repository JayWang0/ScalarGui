namespace ScalarGui.Models;

public class GitNetworkConfig
{
    /// <summary>Maximum size in bytes of the buffer used for HTTP POST data. Default: 500MB.</summary>
    public long HttpPostBuffer { get; set; } = 524_288_000;

    /// <summary>Minimum transfer speed in bytes/sec below which Git aborts. Default: 1000 (1KB/s).</summary>
    public int HttpLowSpeedLimit { get; set; } = 1000;

    /// <summary>Seconds the transfer rate can stay below LowSpeedLimit before aborting. Default: 600 (10 min).</summary>
    public int HttpLowSpeedTime { get; set; } = 600;

    /// <summary>Compression level (0 = none, 9 = max). 0 reduces CPU overhead for large repos.</summary>
    public int CoreCompression { get; set; }

    /// <summary>Fetch negotiation algorithm. "skipping" is faster for large repos.</summary>
    public string FetchNegotiationAlgorithm { get; set; } = "skipping";

    /// <summary>Number of threads for packing. 1 reduces memory pressure.</summary>
    public int PackThreads { get; set; } = 1;

    public Dictionary<string, string> ToConfigEntries() => new()
    {
        ["http.postBuffer"] = HttpPostBuffer.ToString(),
        ["http.lowSpeedLimit"] = HttpLowSpeedLimit.ToString(),
        ["http.lowSpeedTime"] = HttpLowSpeedTime.ToString(),
        ["core.compression"] = CoreCompression.ToString(),
        ["fetch.negotiationAlgorithm"] = FetchNegotiationAlgorithm,
        ["pack.threads"] = PackThreads.ToString()
    };

    public static GitNetworkConfig CreateDefault() => new();
}

public class AppSettings
{
    public string GitPath { get; set; } = "git";
    public string ScalarPath { get; set; } = "scalar";
    public string LastRepoUrl { get; set; } = string.Empty;
    public string LastCloneTargetDirectory { get; set; } = string.Empty;
    public string LastOpenedRepoPath { get; set; } = string.Empty;
    public GitNetworkConfig NetworkConfig { get; set; } = new();
    public int MaxRetries { get; set; } = 5;
    public int BaseRetryDelaySeconds { get; set; } = 2;
    public int MaxRetryDelaySeconds { get; set; } = 60;
    public string Language { get; set; } = "en";
}
