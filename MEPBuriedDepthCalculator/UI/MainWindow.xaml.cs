using System.Windows;
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
            DataContext = new MainViewModel(doc, uidoc, logger);
        }
    }
}
