using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TED_ConfigEditor.Classes
{
    public class ModuleNameToVisibilityConverter: IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return MainWindow.Instance.GetStaticModulesList().Contains((string) value) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
