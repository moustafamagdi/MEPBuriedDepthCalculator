using System;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MEPBuriedDepthCalculator
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            string tabName = "Hatco";
            string panelName = "MEP Tools";

            // 1. Create custom ribbon tab
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                // Tab already exists
            }

            // 2. Create ribbon panel
            RibbonPanel panel = null;
            List<RibbonPanel> panels = application.GetRibbonPanels(tabName);
            foreach (RibbonPanel p in panels)
            {
                if (p.Name == panelName)
                {
                    panel = p;
                    break;
                }
            }

            if (panel == null)
            {
                panel = application.CreateRibbonPanel(tabName, panelName);
            }

            // 3. Add button to the panel
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            
            PushButtonData buttonData = new PushButtonData(
                "MEPBuriedDepthCalculator",
                "Buried Depth\nCalculator",
                assemblyPath,
                "MEPBuriedDepthCalculator.Commands.LaunchCommand");

            buttonData.ToolTip = "Calculate the vertical depth of buried MEP elements below Finished Ground Toposolids.";
            
            // Note: You can add an icon here later by setting buttonData.LargeImage
            
            panel.AddItem(buttonData);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
