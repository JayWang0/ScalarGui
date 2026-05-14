using ScalarGui.Models;

namespace ScalarGui.Services;

public class TreeBrowserService(GitCommandService git)
{
    public async Task<List<string>> GetDirectoriesAsync(string path = "", CancellationToken ct = default)
    {
        var result = await git.LsTreeDirectoriesAsync(path, ct);
        if (!result.Success) return [];

        var lines = result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (!string.IsNullOrEmpty(path))
            lines = lines.Select(l => NormalizeDirectoryPath(path, l)).ToList();

        return lines;
    }

    private static string NormalizeDirectoryPath(string parentPath, string gitOutputPath)
    {
        var normalizedOutput = gitOutputPath.TrimEnd('/');
        var normalizedParent = parentPath.TrimEnd('/');

        if (string.IsNullOrEmpty(normalizedOutput))
            return normalizedParent;

        if (normalizedOutput.Equals(normalizedParent, StringComparison.Ordinal)
            || normalizedOutput.StartsWith($"{normalizedParent}/", StringComparison.Ordinal))
        {
            return normalizedOutput;
        }

        return $"{normalizedParent}/{normalizedOutput}";
    }

    /// <summary>
    /// Recursively discover all subdirectories under rootPath using BFS.
    /// Returns paths sorted deepest-first (leaf directories before parents).
    /// </summary>
    public async Task<List<string>> GetAllDirectoriesRecursiveAsync(
        string rootPath, CancellationToken ct = default, Action<string>? onDiscovered = null)
    {
        var allDirs = new List<string>();
        var queue = new Queue<string>();
        queue.Enqueue(rootPath);

        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var current = queue.Dequeue();
            var children = await GetDirectoriesAsync(current, ct);
            foreach (var child in children)
            {
                allDirs.Add(child);
                queue.Enqueue(child);
                onDiscovered?.Invoke(child);
            }
        }

        // Sort deepest first (by number of '/' separators descending), then alphabetically
        allDirs.Sort((a, b) =>
        {
            int depthA = a.Count(c => c == '/');
            int depthB = b.Count(c => c == '/');
            if (depthB != depthA) return depthB.CompareTo(depthA);
            return string.Compare(a, b, StringComparison.Ordinal);
        });

        return allDirs;
    }

    public async Task<int> EstimateFileCountAsync(string path, CancellationToken ct = default)
    {
        var result = await git.LsTreeRecursiveCountAsync(path, ct);
        if (!result.Success) return -1;

        return result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    public async Task<TreeNode> BuildTreeNodeAsync(string path, CancellationToken ct = default)
    {
        var name = string.IsNullOrEmpty(path) ? "/" : System.IO.Path.GetFileName(path);
        var node = new TreeNode
        {
            Name = name,
            FullPath = path
        };

        var subdirs = await GetDirectoriesAsync(path, ct);
        if (subdirs.Count > 0)
        {
            // Add a dummy child for lazy loading
            node.Children.Add(TreeNode.CreateDummy());
        }

        return node;
    }

    public async Task LoadChildrenAsync(TreeNode parentNode, CancellationToken ct = default)
    {
        if (!parentNode.HasDummyChild) return;

        parentNode.IsLoading = true;
        parentNode.Children.Clear();

        try
        {
            var subdirs = await GetDirectoriesAsync(parentNode.FullPath, ct);
            foreach (var dir in subdirs)
            {
                var child = new TreeNode
                {
                    Name = System.IO.Path.GetFileName(dir),
                    FullPath = dir
                };

                // Check for grandchildren (for the expand arrow)
                var grandchildren = await GetDirectoriesAsync(dir, ct);
                if (grandchildren.Count > 0)
                    child.Children.Add(TreeNode.CreateDummy());

                parentNode.Children.Add(child);
            }
        }
        finally
        {
            parentNode.IsLoading = false;
        }
    }
}
