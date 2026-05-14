using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace ScalarGui.Models;

public partial class TreeNode : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _fullPath = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _estimatedFileCount;

    public ObservableCollection<TreeNode> Children { get; } = [];

    /// <summary>
    /// Placeholder node used for lazy loading — indicates children haven't been fetched yet.
    /// </summary>
    public bool HasDummyChild => Children.Count == 1 && Children[0].Name == "__dummy__";

    public static TreeNode CreateDummy() => new() { Name = "__dummy__" };
}
