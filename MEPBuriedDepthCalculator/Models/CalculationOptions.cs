using System.Collections.Generic;

namespace MEPBuriedDepthCalculator.Models
{
    public enum SelectionMode
    {
        CurrentSelection,
        PickElements,
        CurrentView,
        EntireModel
    }

    public enum DisplayUnitTypeOption
    {
        ProjectUnits,
        Millimeters,
        Centimeters,
        Meters,
        Feet,
        Inches
    }

    public class CalculationOptions
    {
        public SelectionMode SelectionMode { get; set; } = SelectionMode.CurrentSelection;
        public ElementId SelectedLinkInstanceId { get; set; }
        public string SelectedLinkName { get; set; }
        public DisplayUnitTypeOption DisplayUnit { get; set; } = DisplayUnitTypeOption.ProjectUnits;
        public bool DebugMode { get; set; } = false;
    }
}
