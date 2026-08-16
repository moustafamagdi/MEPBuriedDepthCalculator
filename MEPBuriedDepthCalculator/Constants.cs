using System;

namespace MEPBuriedDepthCalculator
{
    public static class Constants
    {
        public const string Version = "1.0.0.0";
        public const string AddInName = "MEP Buried Depth Calculator";

        // Parameter Names
        public const string ParamStartGroundElevation = "Start Ground Elevation";
        public const string ParamStartDepth = "Start Depth";
        public const string ParamEndGroundElevation = "End Ground Elevation";
        public const string ParamEndDepth = "End Depth";

        // Tolerances
        public const double VerticalToleranceFeet = 0.001; // ~0.3mm tolerance for vertical check
        public const double GeometricTolerance = 1e-6;

        // Categories
        public static readonly string[] SupportedCategories = new[]
        {
            "Pipes",
            "Ducts",
            "Conduits"
        };
    }
}
