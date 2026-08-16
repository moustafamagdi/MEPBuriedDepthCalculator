using Autodesk.DB;

namespace MEPBuriedDepthCalculator.Models
{
    public class EndpointCalculationResult
    {
        public bool IsValid { get; set; }
        public XYZ HostPoint { get; set; }
        public XYZ LinkPoint { get; set; }
        public double GroundElevation { get; set; }
        public double BottomElevation { get; set; }
        public double Depth { get; set; }
        public Autodesk.DB.ElementId SelectedToposolidId { get; set; }
        public int CandidateCount { get; set; }
        public string Warning { get; set; }
        public string Error { get; set; }
    }
}
