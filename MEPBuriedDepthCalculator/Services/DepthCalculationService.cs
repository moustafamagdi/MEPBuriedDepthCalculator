using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using MEPBuriedDepthCalculator.Logging;
using MEPBuriedDepthCalculator.Models;
using MEPBuriedDepthCalculator.Utilities;

namespace MEPBuriedDepthCalculator.Services
{
    public class CalculationSummary
    {
        public int TotalSelected { get; set; }
        public int Processed { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int Warnings { get; set; }
        public int Errors { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class DepthCalculationService
    {
        private readonly ILogger _logger;
        private readonly ToposolidService _toposolidService;
        private readonly BottomElevationService _bottomService;

        public DepthCalculationService(ILogger logger)
        {
            _logger = logger;
            _toposolidService = new ToposolidService(logger);
            _bottomService = new BottomElevationService(logger);
        }

        public List<ElementCalculationResult> CalculateAndApply(Document doc, List<Element> elements, CalculationOptions options, out CalculationSummary summary)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var results = new List<ElementCalculationResult>();

            int processedCount = 0;
            int updatedCount = 0;
            int skippedCount = 0;
            int warningCount = 0;
            int errorCount = 0;

            LinkedModelInfo selectedLink = null;
            if (options.SelectedLinkInstanceId != null && options.SelectedLinkInstanceId != ElementId.InvalidElementId)
            {
                var linkService = new LinkedModelService(_logger);
                var links = linkService.GetRevitLinks(doc);
                selectedLink = links.Find(l => l.InstanceId == options.SelectedLinkInstanceId);
            }

            if (selectedLink == null || selectedLink.LinkedDocument == null)
            {
                _logger.Error("CalculationEngine", "No valid Revit Link with Toposolids selected.");
                stopwatch.Stop();
                summary = new CalculationSummary { TotalSelected = elements.Count, Duration = stopwatch.Elapsed };
                return results;
            }

            foreach (var elem in elements)
            {
                var elemResult = new ElementCalculationResult
                {
                    ElementId = elem.Id,
                    CategoryName = elem.Category?.Name ?? "Unknown",
                    ElementTypeName = doc.GetElement(elem.GetTypeId())?.Name ?? "Unknown"
                };

                try
                {
                    // 1. Get LocationCurve
                    if (!(elem.Location is LocationCurve locCurve) || locCurve.Curve == null)
                    {
                        elemResult.Status = CalculationStatus.SkippedNoCurve;
                        skippedCount++;
                        _logger.Info("EndpointCalculation", $"Element {elem.Id} skipped: No valid LocationCurve.");
                        results.Add(elemResult);
                        continue;
                    }

                    XYZ startPoint = locCurve.Curve.GetEndPoint(0);
                    XYZ endPoint = locCurve.Curve.GetEndPoint(1);

                    // 2. Check verticality
                    if (GeometryUtils.IsVertical(startPoint, endPoint))
                    {
                        elemResult.Status = CalculationStatus.SkippedVertical;
                        skippedCount++;
                        _logger.Info("EndpointCalculation", $"Element {elem.Id} skipped: Element is vertical.");
                        results.Add(elemResult);
                        continue;
                    }

                    // 3. Calculate Bottom Elevation and Ground Elevation for Start and End
                    var startDim = _bottomService.CalculateBottomElevation(elem, startPoint);
                    var endDim = _bottomService.CalculateBottomElevation(elem, endPoint);

                    elemResult.ElementSize = startDim.SizeValue;

                    // Start endpoint calculation
                    var startGround = _toposolidService.FindNearestUpperGround(selectedLink.LinkedDocument, selectedLink.Transform, startPoint, startDim.BottomElevation);
                    var startEndpointRes = new EndpointCalculationResult
                    {
                        HostPoint = startPoint,
                        BottomElevation = startDim.BottomElevation
                    };

                    if (startGround != null)
                    {
                        startEndpointRes.IsValid = true;
                        startEndpointRes.GroundElevation = startGround.GroundElevation;
                        startEndpointRes.SelectedToposolidId = startGround.ToposolidId;
                        startEndpointRes.Depth = startGround.GroundElevation - startDim.BottomElevation;
                    }
                    else
                    {
                        startEndpointRes.IsValid = false;
                        startEndpointRes.Warning = "No valid Toposolid surface found above start endpoint.";
                    }
                    elemResult.StartResult = startEndpointRes;

                    // End endpoint calculation
                    var endGround = _toposolidService.FindNearestUpperGround(selectedLink.LinkedDocument, selectedLink.Transform, endPoint, endDim.BottomElevation);
                    var endEndpointRes = new EndpointCalculationResult
                    {
                        HostPoint = endPoint,
                        BottomElevation = endDim.BottomElevation
                    };

                    if (endGround != null)
                    {
                        endEndpointRes.IsValid = true;
                        endEndpointRes.GroundElevation = endGround.GroundElevation;
                        endEndpointRes.SelectedToposolidId = endGround.ToposolidId;
                        endEndpointRes.Depth = endGround.GroundElevation - endDim.BottomElevation;
                    }
                    else
                    {
                        endEndpointRes.IsValid = false;
                        endEndpointRes.Warning = "No valid Toposolid surface found above end endpoint.";
                    }
                    elemResult.EndResult = endEndpointRes;

                    processedCount++;
                    elemResult.Status = CalculationStatus.Success;

                    LogContext.LogElementCalculation(_logger, elemResult);
                }
                catch (Exception ex)
                {
                    errorCount++;
                    elemResult.Status = CalculationStatus.Error;
                    elemResult.Errors.Add(ex.Message);
                    _logger.Error("CalculationEngine", $"Error calculating element {elem.Id}", ex, elem.Id.IntegerValue);
                }

                results.Add(elemResult);
            }

            // Phase 2: Write valid results to parameters in a Revit Transaction
            using (Transaction t = new Transaction(doc, "Calculate and Update MEP Buried Depths"))
            {
                t.Start();

                foreach (var res in results)
                {
                    if (res.Status != CalculationStatus.Success) continue;

                    Element elem = doc.GetElement(res.ElementId);
                    if (elem == null) continue;

                    bool anyWritten = false;

                    // Start Ground Elevation
                    if (res.StartResult != null && res.StartResult.IsValid)
                    {
                        SetParameterValue(elem, Constants.ParamStartGroundElevation, res.StartResult.GroundElevation, ref res.StartElevationUpdated);
                        SetParameterValue(elem, Constants.ParamStartDepth, res.StartResult.Depth, ref res.StartDepthUpdated);
                        anyWritten = true;
                    }

                    // End Ground Elevation
                    if (res.EndResult != null && res.EndResult.IsValid)
                    {
                        SetParameterValue(elem, Constants.ParamEndGroundElevation, res.EndResult.GroundElevation, ref res.EndElevationUpdated);
                        SetParameterValue(elem, Constants.ParamEndDepth, res.EndResult.Depth, ref res.EndDepthUpdated);
                        anyWritten = true;
                    }

                    if (anyWritten)
                    {
                        updatedCount++;
                    }
                }

                t.Commit();
            }

            stopwatch.Stop();
            summary = new CalculationSummary
            {
                TotalSelected = elements.Count,
                Processed = processedCount,
                Updated = updatedCount,
                Skipped = skippedCount,
                Warnings = warningCount,
                Errors = errorCount,
                Duration = stopwatch.Elapsed
            };

            _logger.Info("Summary", $"Calculation completed. Total: {summary.TotalSelected}, Processed: {summary.Processed}, Updated: {summary.Updated}, Skipped: {summary.Skipped}, Errors: {summary.Errors}, Duration: {summary.Duration.TotalSeconds:F2}s");

            return results;
        }

        private void SetParameterValue(Element elem, string paramName, double valueInFeet, ref bool updatedFlag)
        {
            try
            {
                Parameter param = elem.LookupParameter(paramName);
                if (param != null && !param.IsReadOnly && param.StorageType == StorageType.Double)
                {
                    param.Set(valueInFeet);
                    updatedFlag = true;
                    _logger.Debug("ParameterWrite", $"Successfully wrote {paramName} = {valueInFeet:F4} to element {elem.Id}");
                }
                else
                {
                    _logger.Warning("ParameterWrite", $"Parameter '{paramName}' not found, read-only, or incompatible on element {elem.Id}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("ParameterWrite", $"Failed to write parameter '{paramName}' on element {elem.Id}", ex, elem.Id.IntegerValue);
            }
        }
    }
}
