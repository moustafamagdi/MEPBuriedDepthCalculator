using System.Text;
using MEPBuriedDepthCalculator.Models;

namespace MEPBuriedDepthCalculator.Logging
{
    public static class LogContext
    {
        public static void LogElementCalculation(ILogger logger, ElementCalculationResult result)
        {
            if (logger == null) return;

            var sb = new StringBuilder();
            sb.AppendLine($"Element ID: {result.ElementId}");
            sb.AppendLine($"Category: {result.CategoryName}");
            sb.AppendLine($"Type: {result.ElementTypeName}");
            sb.AppendLine($"Status: {result.Status}");

            if (result.StartResult != null)
            {
                sb.AppendLine("Start Endpoint:");
                sb.AppendLine($"  Valid: {result.StartResult.IsValid}");
                if (result.StartResult.HostPoint != null)
                    sb.AppendLine($"  Host XYZ: X={result.StartResult.HostPoint.X:F4}, Y={result.StartResult.HostPoint.Y:F4}, Z={result.StartResult.HostPoint.Z:F4}");
                if (result.StartResult.LinkPoint != null)
                    sb.AppendLine($"  Link XYZ: X={result.StartResult.LinkPoint.X:F4}, Y={result.StartResult.LinkPoint.Y:F4}, Z={result.StartResult.LinkPoint.Z:F4}");
                sb.AppendLine($"  Candidates: {result.StartResult.CandidateCount}");
                sb.AppendLine($"  Ground Elevation: {result.StartResult.GroundElevation:F4}");
                sb.AppendLine($"  Bottom Elevation: {result.StartResult.BottomElevation:F4}");
                sb.AppendLine($"  Depth: {result.StartResult.Depth:F4}");
                if (!string.IsNullOrEmpty(result.StartResult.Warning))
                    sb.AppendLine($"  Warning: {result.StartResult.Warning}");
                if (!string.IsNullOrEmpty(result.StartResult.Error))
                    sb.AppendLine($"  Error: {result.StartResult.Error}");
            }

            if (result.EndResult != null)
            {
                sb.AppendLine("End Endpoint:");
                sb.AppendLine($"  Valid: {result.EndResult.IsValid}");
                if (result.EndResult.HostPoint != null)
                    sb.AppendLine($"  Host XYZ: X={result.EndResult.HostPoint.X:F4}, Y={result.EndResult.HostPoint.Y:F4}, Z={result.EndResult.HostPoint.Z:F4}");
                if (result.EndResult.LinkPoint != null)
                    sb.AppendLine($"  Link XYZ: X={result.EndResult.LinkPoint.X:F4}, Y={result.EndResult.LinkPoint.Y:F4}, Z={result.EndResult.LinkPoint.Z:F4}");
                sb.AppendLine($"  Candidates: {result.EndResult.CandidateCount}");
                sb.AppendLine($"  Ground Elevation: {result.EndResult.GroundElevation:F4}");
                sb.AppendLine($"  Bottom Elevation: {result.EndResult.BottomElevation:F4}");
                sb.AppendLine($"  Depth: {result.EndResult.Depth:F4}");
                if (!string.IsNullOrEmpty(result.EndResult.Warning))
                    sb.AppendLine($"  Warning: {result.EndResult.Warning}");
                if (!string.IsNullOrEmpty(result.EndResult.Error))
                    sb.AppendLine($"  Error: {result.EndResult.Error}");
            }

            logger.Debug("EndpointCalculation", sb.ToString().TrimEnd());
        }
    }
}
