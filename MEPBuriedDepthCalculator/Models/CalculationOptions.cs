using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace MEPBuriedDepthCalculator.Models
{
    public enum SelectionMode
    {
        CurrentSelection,
        PickElements,
        CurrentView,
        EntireModel
    }

    public class CalculationOptions
    {
        public SelectionMode SelectionMode { get; set; } = SelectionMode.CurrentSelection;
        public ElementId SelectedLinkInstanceId { get; set; }
        public string SelectedLinkName { get; set; }
        public bool DebugMode { get; set; } = false;
    }
}
