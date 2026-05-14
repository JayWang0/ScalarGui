using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScalarGui.Models;
using ScalarGui.Services;
using System.Collections.ObjectModel;
using System.IO;

namespace ScalarGui.ViewModels;

public partial class CloneViewModel : ObservableObject
{
    private const string ExistingRepoWarning = "⚠️ Repository already exists at this location";

    private readonly ScalarCommandService _scalar;
    private readonly GitCommandService _git;
    private readonly TaskPersistenceService _persistence;
    private CancellationTokenSource? _cts;
    private bool _isRestoringInputs;

    public CloneViewModel(ScalarCommandService scalar, GitCommandService git, TaskPersistenceService persistence)
    {
        _scalar = scalar;
        _git = git;
        _persistence = persistence;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCloneCommand))]
    private string _repoUrl = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCloneCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenExistingCloneCommand))]
    private string _targetDirectory = string.Empty;

    [ObservableProperty]
    private string _branch = string.Empty;

    [ObservableProperty]
    private bool _singleBranch = true;

    [ObservableProperty]
    private bool _noTags;

    [ObservableProperty]
    private bool _fullClone;

    [ObservableProperty]
    private bool _noSrc;

    [ObservableProperty]
    private bool _useGvfsProtocol;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCloneCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenExistingCloneCommand))]
    private bool _isCloning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenExistingCloneCommand))]
    private bool _isAlreadyCloned;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private double _progress;

    public ObservableCollection<string> LogLines { get; } = [];

    /// <summary>Raised when clone completes. The string is the repo working directory (src path).</summary>
    public event Action<string>? CloneCompleted;

    public async Task LoadAsync()
    {
        var settings = await _persistence.LoadSettingsAsync();

        _isRestoringInputs = true;
        try
        {
            RepoUrl = settings.LastRepoUrl;
            TargetDirectory = settings.LastCloneTargetDirectory;
        }
        finally
        {
            _isRestoringInputs = false;
        }

        UpdateExistingCloneState();
    }

    private bool CanStartClone()
        => !string.IsNullOrWhiteSpace(RepoUrl) && !string.IsNullOrWhiteSpace(TargetDirectory) && !IsCloning;

    private bool CanOpenExistingClone()
        => IsAlreadyCloned && !IsCloning && !string.IsNullOrWhiteSpace(TargetDirectory);

    partial void OnRepoUrlChanged(string value)
    {
        if (_isRestoringInputs)
            return;

        _ = SaveInputsAsync();
    }

    partial void OnTargetDirectoryChanged(string value)
    {
        UpdateExistingCloneState();

        if (_isRestoringInputs)
            return;

        _ = SaveInputsAsync();
    }

    [RelayCommand(CanExecute = nameof(CanStartClone))]
    private async Task StartCloneAsync()
    {
        IsCloning = true;
        _cts = new CancellationTokenSource();
        LogLines.Clear();
        Progress = 0;

        var existingClonePath = GetExistingClonePath();
        if (existingClonePath is not null)
        {
            IsAlreadyCloned = true;
            StatusText = ExistingRepoWarning;
            AppendLog($"⚠️ Repository already exists at \"{existingClonePath}\". Continuing with clone.");
        }

        StatusText = "Cloning...";

        var options = new CloneOptions
        {
            Branch = string.IsNullOrWhiteSpace(Branch) ? null : Branch,
            SingleBranch = SingleBranch,
            NoTags = NoTags,
            FullClone = FullClone,
            NoSrc = NoSrc,
            UseGvfsProtocol = UseGvfsProtocol
        };

        try
        {
            await SaveInputsAsync();
            AppendLog($"Starting: scalar clone \"{RepoUrl}\" \"{TargetDirectory}\"");

            var result = await _scalar.CloneAsync(
                RepoUrl, TargetDirectory, options,
                line => System.Windows.Application.Current.Dispatcher.Invoke(() => AppendLog(line)),
                _cts.Token);

            if (result.Success)
            {
                StatusText = "Clone completed!";
                Progress = 100;
                AppendLog("✅ Clone completed successfully.");

                var srcPath = NoSrc
                    ? TargetDirectory
                    : Path.Combine(TargetDirectory, "src");

                CloneCompleted?.Invoke(srcPath);
            }
            else
            {
                StatusText = "Clone failed.";
                AppendLog($"❌ Clone failed: {result.Error}");
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Clone cancelled.";
            AppendLog("⚠️ Clone was cancelled.");
        }
        catch (Exception ex)
        {
            StatusText = "Clone error.";
            AppendLog($"❌ Error: {ex.Message}");
        }
        finally
        {
            IsCloning = false;
            _cts?.Dispose();
            _cts = null;
            UpdateExistingCloneState();
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenExistingClone))]
    private void OpenExistingClone()
    {
        var existingClonePath = GetExistingClonePath();
        if (existingClonePath is null)
        {
            UpdateExistingCloneState();
            return;
        }

        StatusText = $"Opening existing repository: {existingClonePath}";
        AppendLog($"Opening existing repository at \"{existingClonePath}\".");
        CloneCompleted?.Invoke(existingClonePath);
    }

    [RelayCommand]
    private void CancelClone()
    {
        _cts?.Cancel();
    }

    [RelayCommand]
    private void BrowseTargetDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select target directory for clone"
        };

        if (dialog.ShowDialog() == true)
            TargetDirectory = dialog.FolderName;
    }

    [RelayCommand]
    private void PasteUrl()
    {
        if (System.Windows.Clipboard.ContainsText())
            RepoUrl = System.Windows.Clipboard.GetText().Trim();
    }

    private async Task SaveInputsAsync()
    {
        var settings = await _persistence.LoadSettingsAsync();
        settings.LastRepoUrl = RepoUrl.Trim();
        settings.LastCloneTargetDirectory = TargetDirectory.Trim();
        await _persistence.SaveSettingsAsync(settings);
    }

    private void UpdateExistingCloneState()
    {
        IsAlreadyCloned = GetExistingClonePath() is not null;

        if (IsCloning)
            return;

        if (IsAlreadyCloned)
        {
            StatusText = ExistingRepoWarning;
        }
        else if (StatusText == ExistingRepoWarning)
        {
            StatusText = "Ready";
        }
    }

    private string? GetExistingClonePath()
    {
        if (string.IsNullOrWhiteSpace(TargetDirectory))
            return null;

        var targetPath = TargetDirectory.Trim();
        var rootGitPath = Path.Combine(targetPath, ".git");
        if (Directory.Exists(rootGitPath) || File.Exists(rootGitPath))
            return targetPath;

        var srcPath = Path.Combine(targetPath, "src");
        var srcGitPath = Path.Combine(srcPath, ".git");
        if (Directory.Exists(srcGitPath) || File.Exists(srcGitPath))
            return srcPath;

        return null;
    }

    private void AppendLog(string line)
    {
        LogLines.Add(line);
        while (LogLines.Count > 2000)
            LogLines.RemoveAt(0);
    }
}
