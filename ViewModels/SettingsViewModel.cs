using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScalarGui.Localization;
using ScalarGui.Models;
using ScalarGui.Services;
using System.Globalization;

namespace ScalarGui.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly GitCommandService _git;
    private readonly TaskPersistenceService _persistence;
    private AppSettings _loadedSettings = new();

    public SettingsViewModel(GitCommandService git, TaskPersistenceService persistence)
    {
        _git = git;
        _persistence = persistence;
    }

    [ObservableProperty] private string _gitPath = "git";
    [ObservableProperty] private string _scalarPath = "scalar";
    [ObservableProperty] private long _httpPostBuffer = 524_288_000;
    [ObservableProperty] private int _httpLowSpeedLimit = 1000;
    [ObservableProperty] private int _httpLowSpeedTime = 600;
    [ObservableProperty] private int _coreCompression;
    [ObservableProperty] private string _fetchNegotiationAlgorithm = "skipping";
    [ObservableProperty] private int _packThreads = 1;
    [ObservableProperty] private int _maxRetries = 5;
    [ObservableProperty] private int _baseRetryDelaySeconds = 2;
    [ObservableProperty] private int _maxRetryDelaySeconds = 60;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private int _selectedLanguageIndex;

    public (string Code, string DisplayName)[] SupportedLanguages => TranslationSource.SupportedLanguages;

    partial void OnSelectedLanguageIndexChanged(int value)
    {
        if (value < 0 || value >= SupportedLanguages.Length) return;
        var langCode = SupportedLanguages[value].Code;
        var culture = langCode == "en" ? CultureInfo.InvariantCulture : new CultureInfo(langCode);
        TranslationSource.Instance.CurrentCulture = culture;
    }

    public async Task LoadAsync()
    {
        var s = await _persistence.LoadSettingsAsync();
        _loadedSettings = s;
        GitPath = s.GitPath;
        ScalarPath = s.ScalarPath;
        HttpPostBuffer = s.NetworkConfig.HttpPostBuffer;
        HttpLowSpeedLimit = s.NetworkConfig.HttpLowSpeedLimit;
        HttpLowSpeedTime = s.NetworkConfig.HttpLowSpeedTime;
        CoreCompression = s.NetworkConfig.CoreCompression;
        FetchNegotiationAlgorithm = s.NetworkConfig.FetchNegotiationAlgorithm;
        PackThreads = s.NetworkConfig.PackThreads;
        MaxRetries = s.MaxRetries;
        BaseRetryDelaySeconds = s.BaseRetryDelaySeconds;
        MaxRetryDelaySeconds = s.MaxRetryDelaySeconds;

        // Apply saved language
        var langIndex = Array.FindIndex(SupportedLanguages, l => l.Code == s.Language);
        if (langIndex >= 0)
        {
            SelectedLanguageIndex = langIndex;
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        var settings = await _persistence.LoadSettingsAsync();
        settings.GitPath = GitPath;
        settings.ScalarPath = ScalarPath;
        settings.NetworkConfig = new GitNetworkConfig
        {
            HttpPostBuffer = HttpPostBuffer,
            HttpLowSpeedLimit = HttpLowSpeedLimit,
            HttpLowSpeedTime = HttpLowSpeedTime,
            CoreCompression = CoreCompression,
            FetchNegotiationAlgorithm = FetchNegotiationAlgorithm,
            PackThreads = PackThreads
        };
        settings.MaxRetries = MaxRetries;
        settings.BaseRetryDelaySeconds = BaseRetryDelaySeconds;
        settings.MaxRetryDelaySeconds = MaxRetryDelaySeconds;
        settings.Language = SupportedLanguages[SelectedLanguageIndex].Code;

        await _persistence.SaveSettingsAsync(settings);
        _loadedSettings = settings;
        StatusText = "✅ Settings saved!";
    }

    [RelayCommand]
    private async Task ApplyGitConfigAsync()
    {
        var config = new GitNetworkConfig
        {
            HttpPostBuffer = HttpPostBuffer,
            HttpLowSpeedLimit = HttpLowSpeedLimit,
            HttpLowSpeedTime = HttpLowSpeedTime,
            CoreCompression = CoreCompression,
            FetchNegotiationAlgorithm = FetchNegotiationAlgorithm,
            PackThreads = PackThreads
        };

        foreach (var (key, value) in config.ToConfigEntries())
        {
            await _git.ApplyConfigAsync(key, value, global: true);
        }

        StatusText = "✅ Git config applied globally!";
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        var d = GitNetworkConfig.CreateDefault();
        HttpPostBuffer = d.HttpPostBuffer;
        HttpLowSpeedLimit = d.HttpLowSpeedLimit;
        HttpLowSpeedTime = d.HttpLowSpeedTime;
        CoreCompression = d.CoreCompression;
        FetchNegotiationAlgorithm = d.FetchNegotiationAlgorithm;
        PackThreads = d.PackThreads;
        MaxRetries = 5;
        BaseRetryDelaySeconds = 2;
        MaxRetryDelaySeconds = 60;
        StatusText = "Reset to defaults.";
    }
}
