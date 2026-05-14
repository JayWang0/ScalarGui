using System.IO;
using System.Text.Json;
using ScalarGui.Models;

namespace ScalarGui.Services;

public class TaskPersistenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string GetTaskDir(string repoPath)
    {
        var dir = Path.Combine(repoPath, ".scalargui", "tasks");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public async Task SaveTaskAsync(SparseCheckoutTask task)
    {
        var dir = GetTaskDir(task.RepoPath);
        var filePath = Path.Combine(dir, $"{task.Id}.json");
        var json = JsonSerializer.Serialize(task, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<SparseCheckoutTask?> LoadTaskAsync(string repoPath, string taskId)
    {
        var filePath = Path.Combine(GetTaskDir(repoPath), $"{taskId}.json");
        if (!File.Exists(filePath)) return null;

        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<SparseCheckoutTask>(json, JsonOptions);
    }

    public async Task<List<SparseCheckoutTask>> ListTasksAsync(string repoPath)
    {
        var dir = GetTaskDir(repoPath);
        if (!Directory.Exists(dir)) return [];

        var tasks = new List<SparseCheckoutTask>();
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var task = JsonSerializer.Deserialize<SparseCheckoutTask>(json, JsonOptions);
                if (task != null) tasks.Add(task);
            }
            catch { /* skip corrupt files */ }
        }

        return tasks.OrderByDescending(t => t.CreatedAt).ToList();
    }

    public Task DeleteTaskAsync(string repoPath, string taskId)
    {
        var filePath = Path.Combine(GetTaskDir(repoPath), $"{taskId}.json");
        if (File.Exists(filePath))
            File.Delete(filePath);
        return Task.CompletedTask;
    }

    // App settings persistence
    private static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScalarGui", "settings.json");

    public async Task SaveSettingsAsync(Models.AppSettings settings)
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(SettingsPath, json);
    }

    public async Task<Models.AppSettings> LoadSettingsAsync()
    {
        if (!File.Exists(SettingsPath))
            return new Models.AppSettings();

        try
        {
            var json = await File.ReadAllTextAsync(SettingsPath);
            return JsonSerializer.Deserialize<Models.AppSettings>(json, JsonOptions)
                   ?? new Models.AppSettings();
        }
        catch
        {
            return new Models.AppSettings();
        }
    }
}
