using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ScalarGui.Models;

namespace ScalarGui.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            SubDirStatus.Done => Brushes.Green,
            SubDirStatus.Failed or SubDirStatus.Blocked => Brushes.Red,
            SubDirStatus.InProgress => Brushes.DodgerBlue,
            SubDirStatus.Pending => Brushes.Gray,
            SubDirStatus.Skipped => Brushes.Orange,
            Models.TaskStatus.Completed => Brushes.Green,
            Models.TaskStatus.Failed => Brushes.Red,
            Models.TaskStatus.Running => Brushes.DodgerBlue,
            Models.TaskStatus.Pending => Brushes.Gray,
            Models.TaskStatus.Cancelled => Brushes.Orange,
            _ => Brushes.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not true;
}
