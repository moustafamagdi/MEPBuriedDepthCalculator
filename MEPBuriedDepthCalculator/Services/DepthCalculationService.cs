using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
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
        private readonly SharedParameterService _paramService;

        public DepthCalculationService(ILogger logger)
        {
            _logger = logger;
            _toposolidService = new ToposolidService(logger);
            _bottomService = new BottomElevationService(logger);
            _paramService = new SharedParameterService(logger);
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

            // 1. Get Selected Link
            var linkService = new LinkedModelService(_logger);
            var links = linkService.GetRevitLinks(doc);
            var selectedLink = links.FirstOrDefault(l => l.InstanceId.Value == options.SelectedLinkInstanceId.Value);

            if (selectedLink == null || selectedLink.LinkedDocument == null)
            {
                _logger.Error("CalculationEngine", "Selected link or linked document is unavailable.");
                stopwatch.Stop();
                summary = new CalculationSummary { TotalSelected = elements.Count, Errors = elements.Count, Duration = stopwatch.Elapsed };
                TaskDialog.Show(Constants.AddInName, "Error: The selected Revit Link is no longer available or is unloaded.");
                return results;
            }

            // 2. Initialize Toposolid Cache (Performance optimization)
            _toposolidService.InitializeCache(selectedLink.LinkedDocument);

            // 3. Auto-ensure shared parameters exist
            if (!_paramService.EnsureSharedParametersExist(doc, out string paramMsg))
            {
                _logger.Warning("CalculationEngine", $"Parameter verification failed: {paramMsg}");
            }

            // 4. Calculation Phase
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
                    if (!(elem.Location is LocationCurve locCurve) || locCurve.Curve == null)
                    {
                        elemResult.Status = CalculationStatus.SkippedNoCurve;
                        skippedCount++;
                        results.Add(elemResult);
                        continue;
                    }

                    XYZ startPoint = locCurve.Curve.GetEndPoint(0);
                    XYZ endPoint = locCurve.Curve.GetEndPoint(1);

                    if (GeometryUtils.IsVertical(startPoint, endPoint))
                    {
                        elemResult.Status = CalculationStatus.SkippedVertical;
                        skippedCount++;
                        results.Add(elemResult);
                        continue;
                    }

                    var startDim = _bottomService.CalculateBottomElevation(elem, startPoint);
                    var endDim = _bottomService.CalculateBottomElevation(elem, endPoint);
                    elemResult.ElementSize = startDim.SizeValue;

                    // Start Point
                    double? startGroundZ = _toposolidService.FindNearestUpperGround(startPoint, selectedLink.Transform);
                    var startRes = new EndpointCalculationResult { HostPoint = startPoint, BottomElevation = startDim.BottomElevation };
                    if (startGroundZ.HasValue)
                    {
                        startRes.IsValid = true;
                        startRes.GroundElevation = startGroundZ.Value;
                        startRes.Depth = startGroundZ.Value - startDim.BottomElevation;
                    }
                    else
                    {
                        startRes.IsValid = false;
                        startRes.Warning = "No valid Toposolid surface found above start endpoint.";
                        warningCount++;
                    }
                    elemResult.StartResult = startRes;

                    // End Point
                    double? endGroundZ = _toposolidService.FindNearestUpperGround(endPoint, selectedLink.Transform);
                    var endRes = new EndpointCalculationResult { HostPoint = endPoint, BottomElevation = endDim.BottomElevation };
                    if (endGroundZ.HasValue)
                    {
                        endRes.IsValid = true;
                        endRes.GroundElevation = endGroundZ.Value;
                        endRes.Depth = endGroundZ.Value - endDim.BottomElevation;
                    }
                    else
                    {
                        endRes.IsValid = false;
                        endRes.Warning = "No valid Toposolid surface found above end endpoint.";
                        warningCount++;
                    }
                    elemResult.EndResult = endRes;

                    processedCount++;
                    elemResult.Status = CalculationStatus.Success;
                    LogContext.LogElementCalculation(_logger, elemResult);
                }
                catch (Exception ex)
                {
                    errorCount++;
                    elemResult.Status = CalculationStatus.Error;
                    elemResult.Errors.Add(ex.Message);
                    _logger.Error("CalculationEngine", $"Error calculating element {elem.Id}", ex, elem.Id.Value);
                }
                results.Add(elemResult);
            }

            // 5. Write Phase
            using (Transaction t = new Transaction(doc, "Update MEP Buried Depths"))
            {
                t.Start();
                foreach (var res in results)
                {
                    if (res.Status != CalculationStatus.Success) continue;
                    Element elem = doc.GetElement(res.ElementId);
                    if (elem == null) continue;

                    bool anyWritten = false;
                    if (res.StartResult != null && res.StartResult.IsValid)
                    {
                        if (SetParameterValue(elem, Constants.ParamStartGroundElevation, res.StartResult.GroundElevation)) anyWritten = true;
                        if (SetParameterValue(elem, Constants.ParamStartDepth, res.StartResult.Depth)) anyWritten = true;
                    }
                    if (res.EndResult != null && res.EndResult.IsValid)
                    {
                        if (SetParameterValue(elem, Constants.ParamEndGroundElevation, res.EndResult.GroundElevation)) anyWritten = true;
                        if (SetParameterValue(elem, Constants.ParamEndDepth, res.EndResult.Depth)) anyWritten = true;
                    }
                    if (anyWritten) updatedCount++;
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

            _logger.Info("Summary", $"Completed: {summary.Updated} updated, {summary.Skipped} skipped, {summary.Errors} errors in {summary.Duration.TotalSeconds:F2}s");
            return results;
        }

        private bool SetParameterValue(Element elem, string paramName, double valueInFeet)
        {
            try
            {
                Parameter param = elem.LookupParameter(paramName);
                if (param != null && !param.IsReadOnly && param.StorageType == StorageType.Double)
                {
                    param.Set(valueInFeet);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("ParameterWrite", $"Failed to write {paramName} to {elem.Id}", ex, elem.Id.Value);
            }
            return false;
        }
    }
}
