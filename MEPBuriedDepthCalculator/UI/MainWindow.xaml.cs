using System;
using System.Windows;
using System.Windows.Data;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MEPBuriedDepthCalculator.Logging;

namespace MEPBuriedDepthCalculator.UI
{
    public partial class MainWindow : Window
    {
        public MainWindow(Document doc, UIDocument uidoc, ILogger logger)
        {
            InitializeComponent();
            DataContext = new MainViewModel(doc, uidoc, logger, this);
        }
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool boolValue) return !boolValue;
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool boolValue) return !boolValue;
            return true;
        }
    }
}
