using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScalarGui.Models;
using ScalarGui.Services;
using System.Collections.ObjectModel;

namespace ScalarGui.ViewModels;

public partial class SparseCheckoutViewModel : ObservableObject
{
    private readonly GitCommandService _git;
    private readonly SparseCheckoutService _sparseCheckout;
    private readonly TreeBrowserService _treeBrowser;
    private readonly TaskPersistenceService _persistence;
    private CancellationTokenSource? _cts;

    public SparseCheckoutViewModel(
        GitCommandService git,
        SparseCheckoutService sparseCheckout,
        TreeBrowserService treeBrowser,
        TaskPersistenceService persistence)
    {
        _git = git;
        _sparseCheckout = sparseCheckout;
        _treeBrowser = treeBrowser;
        _persistence = persistence;

        _sparseCheckout.LogMessage += msg =>
            System.Windows.Application.Current.Dispatcher.Invoke(() => AppendLog(msg));
        _sparseCheckout.ProgressChanged += task =>
            System.Windows.Application.Current.Dispatcher.Invoke(() => UpdateProgress(task));
    }

    [ObservableProperty]
    private string _repoPath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddSelectedDirectoryCommand))]
    private bool _isWorking;

    [ObservableProperty]
    private string _statusText = "Select a directory to add";

    [ObservableProperty]
    private double _overallProgress;

    [ObservableProperty]
    private int _doneCount;

    [ObservableProperty]
    private int _failedCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private string _currentSubDir = string.Empty;

    [ObservableProperty]
    private SparseCheckoutTask? _currentTask;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddSelectedDirectoryCommand))]
    private TreeNode? _selectedTreeNode;

    public ObservableCollection<TreeNode> TreeRoots { get; } = [];
    public ObservableCollection<string> CurrentPatterns { get; } = [];
    public ObservableCollection<string> LogLines { get; } = [];
    public ObservableCollection<SparseCheckoutTask> PreviousTasks { get; } = [];

    public async Task InitializeAsync(string repoPath)
    {
        RepoPath = repoPath;
        _git.SetWorkingDirectory(repoPath);

        await RefreshCurrentPatternsAsync();
        await LoadTreeRootsAsync();
        await LoadPreviousTasksAsync();
    }

    [RelayCommand]
    private async Task RefreshCurrentPatternsAsync()
    {
        CurrentPatterns.Clear();
        var result = await _git.SparseCheckoutListAsync();
        if (result.Success)
        {
            foreach (var line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                CurrentPatterns.Add(line);
        }
    }

    private async Task LoadTreeRootsAsync()
    {
        TreeRoots.Clear();
        var dirs = await _treeBrowser.GetDirectoriesAsync("", CancellationToken.None);
        foreach (var dir in dirs)
        {
            var node = new TreeNode
            {
                Name = dir,
                FullPath = dir
            };
            // Check for children
            var children = await _treeBrowser.GetDirectoriesAsync(dir, CancellationToken.None);
            if (children.Count > 0)
                node.Children.Add(TreeNode.CreateDummy());

            TreeRoots.Add(node);
        }
    }

    public async Task OnTreeNodeExpandedAsync(TreeNode node)
    {
        await _treeBrowser.LoadChildrenAsync(node);
    }

    private bool CanAddSelectedDirectory() =>
        SelectedTreeNode != null && !string.IsNullOrEmpty(SelectedTreeNode.FullPath) && !IsWorking;

    [RelayCommand(CanExecute = nameof(CanAddSelectedDirectory))]
    private async Task AddSelectedDirectoryAsync()
    {
        var node = SelectedTreeNode;
        if (node == null || string.IsNullOrEmpty(node.FullPath)) return;

        IsWorking = true;
        _cts = new CancellationTokenSource();
        StatusText = $"Creating incremental task for '{node.FullPath}'...";

        try
        {
            var task = await _sparseCheckout.CreateTaskAsync(RepoPath, node.FullPath, _cts.Token);
            CurrentTask = task;

            StatusText = $"Adding '{node.FullPath}' ({task.TotalCount} subdirectories)...";
            await _sparseCheckout.ExecuteAsync(task, _cts.Token);

            StatusText = task.OverallStatus == Models.TaskStatus.Completed
                ? $"✅ Done: {task.DoneCount}/{task.TotalCount} subdirectories added"
                : $"⚠️ Partial: {task.DoneCount} done, {task.FailedCount} failed";

            await RefreshCurrentPatternsAsync();
            await LoadPreviousTasksAsync();
        }
        catch (OperationCanceledException)
        {
            StatusText = "⏸️ Paused.";
            AppendLog("Operation paused by user.");
        }
        catch (Exception ex)
        {
            StatusText = $"❌ Error: {ex.Message}";
            AppendLog($"Error: {ex.Message}");
        }
        finally
        {
            IsWorking = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private async Task ResumeTaskAsync(SparseCheckoutTask? task)
    {
        if (task == null) return;

        IsWorking = true;
        _cts = new CancellationTokenSource();
        CurrentTask = task;
        StatusText = $"Resuming task for '{task.TargetDirectory}'...";

        try
        {
            await _sparseCheckout.ExecuteAsync(task, _cts.Token);
            StatusText = task.OverallStatus == Models.TaskStatus.Completed
                ? $"✅ Resume complete: {task.DoneCount}/{task.TotalCount}"
                : $"⚠️ {task.DoneCount} done, {task.FailedCount} failed";
            await RefreshCurrentPatternsAsync();
        }
        catch (OperationCanceledException)
        {
            StatusText = "⏸️ Paused.";
        }
        finally
        {
            IsWorking = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private async Task RetryFailedAsync()
    {
        if (CurrentTask == null) return;

        IsWorking = true;
        _cts = new CancellationTokenSource();
        StatusText = "Retrying failed directories...";

        try
        {
            await _sparseCheckout.RetryFailedAsync(CurrentTask, _cts.Token);
            StatusText = CurrentTask.FailedCount == 0
                ? "✅ All retries succeeded!"
                : $"⚠️ {CurrentTask.FailedCount} still failed";
            await RefreshCurrentPatternsAsync();
        }
        catch (OperationCanceledException)
        {
            StatusText = "⏸️ Paused.";
        }
        finally
        {
            IsWorking = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void Pause()
    {
        _cts?.Cancel();
    }

    private async Task LoadPreviousTasksAsync()
    {
        if (string.IsNullOrEmpty(RepoPath)) return;
        PreviousTasks.Clear();
        var tasks = await _persistence.ListTasksAsync(RepoPath);
        foreach (var t in tasks)
            PreviousTasks.Add(t);
    }

    private void UpdateProgress(SparseCheckoutTask task)
    {
        DoneCount = task.DoneCount;
        FailedCount = task.FailedCount;
        TotalCount = task.TotalCount;
        OverallProgress = task.ProgressPercent;

        var current = task.SubDirectories.FirstOrDefault(d => d.Status == SubDirStatus.InProgress);
        CurrentSubDir = current?.Path ?? string.Empty;
    }

    private void AppendLog(string line)
    {
        LogLines.Add(line);
        while (LogLines.Count > 5000)
            LogLines.RemoveAt(0);
    }
}
