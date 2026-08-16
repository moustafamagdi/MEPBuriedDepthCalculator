using System;
using Autodesk.DB;
using Autodesk.DB.Mechanical;
using Autodesk.DB.Plumbing;
using Autodesk.DB.Electrical;
using MEPBuriedDepthCalculator.Logging;

namespace MEPBuriedDepthCalculator.Services
{
    public class ElementDimensionResult
    {
        public double CenterlineElevation { get; set; }
        public double BottomElevation { get; set; }
        public double SizeValue { get; set; }
        public string TypeName { get; set; }
    }

    public class BottomElevationService
    {
        private readonly ILogger _logger;

        public BottomElevationService(ILogger logger)
        {
            _logger = logger;
        }

        public ElementDimensionResult CalculateBottomElevation(Element elem, XYZ endpoint)
        {
            try
            string catName = elem.Category?.Name ?? "";
            double centerlineZ = endpoint.Z;
            double radiusOrHalfHeight = 0.0;
            string typeName = "Unknown";

            // Check element type
            Element elementType = elem.Document.GetElement(elem.GetTypeId());
            if (elementType != null)
            {
                typeName = elementType.Name;
            }

            if (elem is Pipe pipe)
            {
                double outerDiameter = pipe.Diameter;
                radiusOrHalfHeight = outerDiameter / 2.0;
                _logger.Debug("BottomElevation", $"Pipe ID {elem.Id}: Diameter={outerDiameter}, CenterlineZ={centerlineZ}");
            }
            else if (elem is Conduit conduit)
            {
                double diameter = conduit.Diameter;
                radiusOrHalfHeight = diameter / 2.0;
                _logger.Debug("BottomElevation", $"Conduit ID {elem.Id}: Diameter={diameter}, CenterlineZ={centerlineZ}");
            }
            else if (elem is Duct duct)
            {
                // Check duct shape / dimensions (width, height, diameter)
                try
                {
                    // Parameter lookup for width/height/diameter
                    Parameter widthParam = duct.LookupParameter("Width");
                    Parameter heightParam = duct.LookupParameter("Height");
                    Parameter diameterParam = duct.LookupParameter("Diameter");

                    if (heightParam != null && heightParam.HasValue)
                    {
                        double height = heightParam.AsDouble();
                        radiusOrHalfHeight = height / 2.0;
                    }
                    else if (diameterParam != null && diameterParam.HasValue)
                    {
                        double diameter = diameterParam.AsDouble();
                        radiusOrHalfHeight = diameter / 2.0;
                    }
                    else
                    {
                        radiusOrHalfHeight = 0.5; // fallback default
                    }
                }
                catch
                {
                    radiusOrHalfHeight = 0.5;
                }
                _logger.Debug("BottomElevation", $"Duct ID {elem.Id}: HalfHeight={radiusOrHalfHeight}, CenterlineZ={centerlineZ}");
            }
            else
            {
                // Fallback for generic curves
                radiusOrHalfHeight = 0.0;
            }

            double bottomZ = centerlineZ - radiusOrHalfHeight;

            return new ElementDimensionResult
            {
                CenterlineElevation = centerlineZ,
                BottomElevation = bottomZ,
                SizeValue = radiusOrHalfHeight * 2.0,
                TypeName = typeName
            };
        }
    }
}
