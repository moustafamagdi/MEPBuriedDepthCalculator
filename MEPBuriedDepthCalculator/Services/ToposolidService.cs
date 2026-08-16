using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using MEPBuriedDepthCalculator.Logging;
using MEPBuriedDepthCalculator.Models;
using MEPBuriedDepthCalculator.Utilities;

namespace MEPBuriedDepthCalculator.Services
{
    public class GroundCandidate
    {
        public ElementId ToposolidId { get; set; }
        public double GroundElevation { get; set; }
    }

    public class ToposolidService
    {
        private readonly ILogger _logger;

        public ToposolidService(ILogger logger)
        {
            _logger = logger;
        }

        public GroundCandidate FindNearestUpperGround(Document linkedDoc, Transform linkTransform, XYZ hostPoint, double elementBottomZ)
        {
            try
            {
                // 1. Transform host endpoint into linked model coordinate system
                // Inverse transform or transform depending on Revit link coordinate convention.
                // In Revit, linkTransform transforms from link coordinates to host coordinates.
                // Therefore, to convert host point to link coordinates, we use linkTransform.Inverse.
                Transform inverseTransform = linkTransform.Inverse;
                XYZ linkPoint = inverseTransform.OfPoint(hostPoint);

                _logger.Debug("ToposolidGeometry", $"Host point Z={hostPoint.Z:F4} transformed to link point Z={linkPoint.Z:F4}");

                // 2. Collect Toposolids in linked document
                // In Revit 2024, Toposolids are represented by BuiltInCategory.OST_Toposolid or class Toposolid (if available) or generic elements.
                var candidates = new List<GroundCandidate>();

                var collector = new FilteredElementCollector(linkedDoc)
                    .WhereElementIsNotElementType();

                foreach (Element elem in collector)
                {
                    if (elem.Category != null && (elem.Category.Id.Value == (long)BuiltInCategory.OST_Toposolid || elem.Category.Name.Contains("Toposolid")))
                    {
                        double? groundZ = EvaluateToposolidAtPoint(elem, linkPoint);
                        if (groundZ.HasValue)
                        {
                            // Transform ground elevation back to host coordinates if necessary, or evaluate in host Z space.
                            // Since linkTransform handles vertical translation and scale (usually scale=1), we transform the point (linkPoint.X, linkPoint.Y, groundZ.Value) back to host coordinates.
                            XYZ linkGroundPoint = new XYZ(linkPoint.X, linkPoint.Y, groundZ.Value);
                            XYZ hostGroundPoint = linkTransform.OfPoint(linkGroundPoint);

                            // Only consider surfaces located ABOVE or at the MEP endpoint
                            if (hostGroundPoint.Z >= elementBottomZ - 1e-5)
                            {
                                candidates.Add(new GroundCandidate
                                {
                                    ToposolidId = elem.Id,
                                    GroundElevation = hostGroundPoint.Z
                                });
                            }
                        }
                    }
                }

                if (candidates.Count == 0)
                {
                    return null;
                }

                // Select the NEAREST upper surface (minimum ground elevation among those >= elementBottomZ)
                var sortedCandidates = candidates.OrderBy(c => c.GroundElevation).ToList();
                var selected = sortedCandidates.First();

                if (sortedCandidates.Count > 1)
                {
                    _logger.Warning("ToposolidDiscovery", $"Multiple ground surfaces ({sortedCandidates.Count}) detected above element point. Nearest upper surface (ID: {selected.ToposolidId}, Elev: {selected.GroundElevation:F4}) selected.");
                }

                return selected;
            }
            catch (Exception ex)
            {
                _logger.Error("ToposolidGeometry", "Error evaluating Toposolid surface elevation", ex);
                return null;
            }
        }

        private double? EvaluateToposolidAtPoint(Element toposolidElem, XYZ linkPoint)
        {
            try
            {
                // Extract geometry options
                var options = new Options
                {
                    ComputeReferences = true,
                    DetailLevel = ViewDetailLevel.Fine
                };

                GeometryElement geomElem = toposolidElem.get_Geometry(options);
                if (geomElem == null) return null;

                double? highestIntersectZ = null;

                foreach (GeometryObject geomObj in geomElem)
                {
                    if (geomObj is Solid solid && solid.Faces.Size > 0)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            Mesh mesh = face.Triangulate();
                            if (mesh == null) continue;

                            for (int i = 0; i < mesh.NumTriangles; i++)
                            {
                                MeshTriangle triangle = mesh.get_Triangle(i);
                                XYZ p1 = triangle.get_Vertex(0);
                                XYZ p2 = triangle.get_Vertex(1);
                                XYZ p3 = triangle.get_Vertex(2);

                                if (GeometryUtils.IsPointInsideTriangle(linkPoint, p1, p2, p3))
                                {
                                    double? z = GeometryUtils.InterpolateZOnTriangle(linkPoint, p1, p2, p3);
                                    if (z.HasValue)
                                    {
                                        if (!highestIntersectZ.HasValue || z.Value > highestIntersectZ.Value)
                                        {
                                            highestIntersectZ = z.Value;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return highestIntersectZ;
            }
            catch (Exception ex)
            {
                _logger.Debug("ToposolidGeometry", $"Failed to extract triangulation for Toposolid {toposolidElem.Id}: {ex.Message}");
                return null;
            }
        }
    }
}
