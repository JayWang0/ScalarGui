using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScalarGui.Services;
using System.IO;

namespace ScalarGui.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly GitCommandService _git;
    private readonly ScalarCommandService _scalar;
    private readonly TaskPersistenceService _persistence;

    public CloneViewModel Clone { get; }
    public SparseCheckoutViewModel SparseCheckout { get; }
    public SettingsViewModel Settings { get; }
    public SetupViewModel Setup { get; }

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _gitVersion = string.Empty;

    [ObservableProperty]
    private string _scalarVersion = string.Empty;

    [ObservableProperty]
    private string _currentRepoPath = string.Empty;

    [ObservableProperty]
    private int _selectedNavIndex;

    public MainViewModel()
    {
        _git = new GitCommandService();
        _scalar = new ScalarCommandService();
        _persistence = new TaskPersistenceService();

        var treeBrowser = new TreeBrowserService(_git);
        var sparseCheckoutService = new SparseCheckoutService(_git, treeBrowser, _persistence);

        Clone = new CloneViewModel(_scalar, _git, _persistence);
        SparseCheckout = new SparseCheckoutViewModel(_git, sparseCheckoutService, treeBrowser, _persistence);
        Settings = new SettingsViewModel(_git, _persistence);
        Setup = new SetupViewModel(_git, _scalar);
        CurrentView = Setup;

        // When clone finishes, switch to sparse-checkout view
        Clone.CloneCompleted += async repoPath =>
        {
            CurrentRepoPath = repoPath;
            await SparseCheckout.InitializeAsync(repoPath);
            SelectedNavIndex = 2; // Sparse-Checkout
        };
    }

    public async Task InitializeAsync()
    {
        // Load settings
        await Settings.LoadAsync();
        await Clone.LoadAsync();
        _git.Configure(Settings.GitPath);
        _scalar.Configure(Settings.ScalarPath);

        var appSettings = await _persistence.LoadSettingsAsync();
        if (!string.IsNullOrWhiteSpace(appSettings.LastOpenedRepoPath)
            && Directory.Exists(appSettings.LastOpenedRepoPath))
        {
            CurrentRepoPath = appSettings.LastOpenedRepoPath;
            _git.SetWorkingDirectory(appSettings.LastOpenedRepoPath);
            await SparseCheckout.InitializeAsync(appSettings.LastOpenedRepoPath);
        }

        // Detect versions
        var gitResult = await _git.RunAsync(["--version"]);
        if (gitResult.Success)
            GitVersion = gitResult.Output.Trim();

        try
        {
            var scalarResult = await _scalar.ListAsync();
            ScalarVersion = "Scalar available";
        }
        catch
        {
            ScalarVersion = "Scalar not found";
        }

        // If tools are not ready, stay on Setup page
        if (!Setup.AllToolsReady)
        {
            SelectedNavIndex = 0;
        }
        else if (!string.IsNullOrWhiteSpace(CurrentRepoPath))
        {
            SelectedNavIndex = 2; // Sparse-Checkout
        }
        else
        {
            SelectedNavIndex = 1; // Clone
        }
    }

    [RelayCommand]
    private async Task OpenExistingRepoAsync()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select existing repo working directory (the 'src' folder)"
        };

        if (dialog.ShowDialog() == true)
        {
            CurrentRepoPath = dialog.FolderName;
            _git.SetWorkingDirectory(dialog.FolderName);
            await SparseCheckout.InitializeAsync(dialog.FolderName);
            SelectedNavIndex = 2; // Sparse-Checkout
        }
    }

    partial void OnCurrentRepoPathChanged(string value)
    {
        _ = SaveLastOpenedRepoPathAsync(value);
    }

    private async Task SaveLastOpenedRepoPathAsync(string repoPath)
    {
        var settings = await _persistence.LoadSettingsAsync();
        settings.LastOpenedRepoPath = repoPath;
        await _persistence.SaveSettingsAsync(settings);
    }

    partial void OnSelectedNavIndexChanged(int value)
    {
        CurrentView = value switch
        {
            0 => Setup,
            1 => Clone,
            2 => SparseCheckout,
            3 => Settings,
            _ => Setup
        };
    }
}
