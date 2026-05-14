using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScalarGui.Services;
using System.Diagnostics;

namespace ScalarGui.ViewModels;

public partial class SetupViewModel : ObservableObject
{
    private readonly GitCommandService _git;
    private readonly ScalarCommandService _scalar;

    public SetupViewModel(GitCommandService git, ScalarCommandService scalar)
    {
        _git = git;
        _scalar = scalar;
        StatusText = "Checking prerequisites...";

        _ = CheckPrerequisitesAsync();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllToolsReady))]
    private bool _isGitInstalled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllToolsReady))]
    private bool _isScalarInstalled;

    [ObservableProperty]
    private string _gitVersion = string.Empty;

    [ObservableProperty]
    private string _scalarVersion = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckPrerequisitesCommand))]
    private bool _isChecking;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public bool AllToolsReady => IsGitInstalled && IsScalarInstalled;

    private bool CanCheckPrerequisites() => !IsChecking;

    [RelayCommand(CanExecute = nameof(CanCheckPrerequisites))]
    private async Task CheckPrerequisitesAsync()
    {
        if (IsChecking)
        {
            return;
        }

        IsChecking = true;
        StatusText = "Checking prerequisites...";
        GitVersion = string.Empty;
        ScalarVersion = string.Empty;
        IsGitInstalled = false;
        IsScalarInstalled = false;

        try
        {
            await Task.WhenAll(CheckGitAsync(), CheckScalarAsync());
            StatusText = AllToolsReady
                ? "All tools ready! You can proceed."
                : "Please install missing tools to continue.";
        }
        catch (Exception ex)
        {
            StatusText = $"Unable to complete prerequisite check: {ex.Message}";
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand]
    private void OpenGitDownload()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://git-scm.com/downloads",
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private void OpenScalarDownload()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/microsoft/scalar/releases",
            UseShellExecute = true
        });
    }

    private async Task CheckGitAsync()
    {
        try
        {
            var result = await _git.RunAsync(["--version"]);
            IsGitInstalled = result.Success;
            GitVersion = result.Success ? result.Output.Trim() : string.Empty;
        }
        catch
        {
            IsGitInstalled = false;
            GitVersion = string.Empty;
        }
    }

    private async Task CheckScalarAsync()
    {
        try
        {
            await _scalar.ListAsync();
            var version = await _scalar.VersionAsync();
            IsScalarInstalled = version.Success;
            ScalarVersion = version.Success
                ? (string.IsNullOrWhiteSpace(version.Output) ? version.Error : version.Output).Trim()
                : string.Empty;
        }
        catch
        {
            IsScalarInstalled = false;
            ScalarVersion = string.Empty;
        }
    }
}
