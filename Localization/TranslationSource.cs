using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace ScalarGui.Localization;

/// <summary>
/// Singleton that provides localized strings and supports runtime language switching.
/// Bind in XAML via the <see cref="LocExtension"/> markup extension.
/// </summary>
public sealed class TranslationSource : INotifyPropertyChanged
{
    public static TranslationSource Instance { get; } = new();

    private readonly ResourceManager _resourceManager =
        new("ScalarGui.Resources.Strings", typeof(TranslationSource).Assembly);

    private CultureInfo _currentCulture = CultureInfo.CurrentUICulture;

    public string this[string key]
    {
        get
        {
            var value = _resourceManager.GetString(key, _currentCulture);
            return value ?? $"[{key}]";
        }
    }

    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (Equals(_currentCulture, value)) return;
            _currentCulture = value;
            // Notify all bindings that use the indexer
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCulture)));
        }
    }

    /// <summary>
    /// Supported languages: "en" for English, "zh-CN" for Chinese.
    /// </summary>
    public static readonly (string Code, string DisplayName)[] SupportedLanguages =
    [
        ("en", "English"),
        ("zh-CN", "中文")
    ];

    public event PropertyChangedEventHandler? PropertyChanged;
}
