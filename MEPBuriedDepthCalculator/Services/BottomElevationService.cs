using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Electrical;
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
            double centerlineZ = endpoint.Z;
            double radiusOrHalfHeight = 0.0;
            string typeName = "Unknown";

            try
            {
                // Check element type
                ElementId typeId = elem.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    Element elementType = elem.Document.GetElement(typeId);
                    if (elementType != null)
                    {
                        typeName = elementType.Name;
                    }
                }

                if (elem is Pipe pipe)
                {
                    // Use BuiltInParameter for more reliability
                    Parameter diamParam = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_OUTER_DIAMETER);
                    double diameter = (diamParam != null && diamParam.HasValue) ? diamParam.AsDouble() : pipe.Diameter;
                    radiusOrHalfHeight = diameter / 2.0;
                    _logger.Debug("BottomElevation", $"Pipe ID {elem.Id}: Diameter={diameter}, CenterlineZ={centerlineZ}");
                }
                else if (elem is Conduit conduit)
                {
                    Parameter diamParam = conduit.get_Parameter(BuiltInParameter.RBS_CONDUIT_OUTER_DIAM_PARAM);
                    double diameter = (diamParam != null && diamParam.HasValue) ? diamParam.AsDouble() : conduit.Diameter;
                    radiusOrHalfHeight = diameter / 2.0;
                    _logger.Debug("BottomElevation", $"Conduit ID {elem.Id}: Diameter={diameter}, CenterlineZ={centerlineZ}");
                }
                else if (elem is Duct duct)
                {
                    try
                    {
                        Parameter heightParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
                        Parameter diameterParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);

                        if (heightParam != null && heightParam.HasValue)
                        {
                            radiusOrHalfHeight = heightParam.AsDouble() / 2.0;
                        }
                        else if (diameterParam != null && diameterParam.HasValue)
                        {
                            radiusOrHalfHeight = diameterParam.AsDouble() / 2.0;
                        }
                        else
                        {
                            radiusOrHalfHeight = 0.0;
                        }
                    }
                    catch
                    {
                        radiusOrHalfHeight = 0.0;
                    }
                    _logger.Debug("BottomElevation", $"Duct ID {elem.Id}: HalfHeight={radiusOrHalfHeight}, CenterlineZ={centerlineZ}");
                }
                else
                {
                    // Fallback for generic curves
                    radiusOrHalfHeight = 0.0;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("BottomElevation", $"Error calculating bottom elevation for element {elem.Id}", ex);
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
