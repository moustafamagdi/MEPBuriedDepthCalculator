using System.Collections.Generic;
using Autodesk.DB;

namespace MEPBuriedDepthCalculator.Models
{
    public enum CalculationStatus
    {
        Success,
        SkippedVertical,
        SkippedNoCurve,
        Error,
        PartialSuccess
    }

    public class ElementCalculationResult
    {
        public Autodesk.DB.ElementId ElementId { get; set; }
        public string CategoryName { get; set; }
        public string ElementTypeName { get; set; }
        public double ElementSize { get; set; }
        public CalculationStatus Status { get; set; }
        public EndpointCalculationResult StartResult { get; set; }
        public EndpointCalculationResult EndResult { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
        public bool StartElevationUpdated { get; set; }
        public bool StartDepthUpdated { get; set; }
        public bool EndElevationUpdated { get; set; }
        public bool EndDepthUpdated { get; set; }
    }
}
