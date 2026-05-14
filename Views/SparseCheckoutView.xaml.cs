using System.Windows;
using System.Windows.Controls;
using ScalarGui.Models;
using ScalarGui.ViewModels;

namespace ScalarGui.Views;

public partial class SparseCheckoutView : UserControl
{
    public SparseCheckoutView()
    {
        InitializeComponent();
    }

    private async void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { DataContext: TreeNode node } && node.HasDummyChild)
        {
            if (DataContext is SparseCheckoutViewModel vm)
            {
                await vm.OnTreeNodeExpandedAsync(node);
            }
        }
    }

    private void DirTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is SparseCheckoutViewModel vm)
        {
            vm.SelectedTreeNode = e.NewValue as TreeNode;
        }
    }

    private async void PreviousTask_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBox { SelectedItem: SparseCheckoutTask task } && DataContext is SparseCheckoutViewModel vm)
        {
            await vm.ResumeTaskCommand.ExecuteAsync(task);
        }
    }
}
