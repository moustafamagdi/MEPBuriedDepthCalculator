using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using MEPBuriedDepthCalculator.Logging;
using MEPBuriedDepthCalculator.UI;

namespace MEPBuriedDepthCalculator.Commands
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
    public class LaunchCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var logger = new FileLogger(debugMode: true);
            try
            {
                logger.Info("Initialization", "MEP Buried Depth Calculator add-in launched.");
                
                UIApplication uiapp = commandData.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;
                Document doc = uidoc.Document;

                logger.Info("Initialization", $"Revit Version: 2024. Active Document: {doc.Title}");

                var window = new MainWindow(doc, uidoc, logger);
                window.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                logger.Fatal("Initialization", "Fatal error launching add-in", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
