using System;
using System.Globalization;
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

    /// <summary>Inverts a bool — used to disable input controls while IsBusy is true.</summary>
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;
    }
}
