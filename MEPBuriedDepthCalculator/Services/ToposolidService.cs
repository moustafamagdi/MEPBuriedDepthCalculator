using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using MEPBuriedDepthCalculator.Logging;
using MEPBuriedDepthCalculator.Utilities;

namespace MEPBuriedDepthCalculator.Services
{
    public class ToposolidCache
    {
        public ElementId Id { get; set; }
        public List<List<XYZ>> Triangles { get; set; } = new List<List<XYZ>>();
    }

    public class ToposolidService
    {
        private readonly ILogger _logger;
        private List<ToposolidCache> _cachedToposolids;
        private Document _lastDoc;

        public ToposolidService(ILogger logger)
        {
            _logger = logger;
        }

        public void InitializeCache(Document linkDoc)
        {
            if (_lastDoc == linkDoc && _cachedToposolids != null) return;

            _cachedToposolids = new List<ToposolidCache>();
            _lastDoc = linkDoc;

            var collector = new FilteredElementCollector(linkDoc)
                .WhereElementIsNotElementType();

            foreach (Element elem in collector)
            {
                if (elem.Category != null && (elem.Category.Id.Value == (long)BuiltInCategory.OST_Toposolid || elem.Category.Name.Contains("Toposolid")))
                {
                    var cache = new ToposolidCache { Id = elem.Id };
                    var opt = new Options { DetailLevel = ViewDetailLevel.Fine };
                    GeometryElement geo = elem.get_Geometry(opt);
                    if (geo == null) continue;

                    foreach (GeometryObject obj in geo)
                    {
                        if (obj is Solid solid && solid.Volume > 0)
                        {
                            foreach (Face face in solid.Faces)
                            {
                                // Only process top-facing or near-top-facing surfaces
                                if (face.ComputeNormal(new UV(0.5, 0.5)).Z > 0)
                                {
                                    Mesh mesh = face.Triangulate();
                                    if (mesh == null) continue;

                                    for (int i = 0; i < mesh.NumTriangles; i++)
                                    {
                                        MeshTriangle tri = mesh.get_Triangle(i);
                                        cache.Triangles.Add(new List<XYZ> { tri.get_Vertex(0), tri.get_Vertex(1), tri.get_Vertex(2) });
                                    }
                                }
                            }
                        }
                    }
                    if (cache.Triangles.Count > 0)
                    {
                        _cachedToposolids.Add(cache);
                    }
                }
            }
            _logger.Info("ToposolidService", $"Initialized cache with {_cachedToposolids.Count} toposolids and {(_cachedToposolids.Sum(c => c.Triangles.Count))} triangles.");
        }

        public double? FindNearestUpperGround(XYZ hostPoint, Transform linkTransform)
        {
            if (_cachedToposolids == null || _cachedToposolids.Count == 0) return null;

            XYZ linkPoint = linkTransform.Inverse.OfPoint(hostPoint);
            double? bestGroundZ = null;

            foreach (var cache in _cachedToposolids)
            {
                foreach (var tri in cache.Triangles)
                {
                    if (GeometryUtils.IsPointInTriangleXY(linkPoint, tri[0], tri[1], tri[2]))
                    {
                        double? z = GeometryUtils.InterpolateZOnTriangle(linkPoint, tri[0], tri[1], tri[2]);
                        if (z.HasValue)
                        {
                            // Ground must be above or at the point level
                            if (z.Value >= linkPoint.Z)
                            {
                                if (!bestGroundZ.HasValue || z.Value < bestGroundZ.Value)
                                {
                                    bestGroundZ = z.Value;
                                }
                            }
                        }
                    }
                }
            }

            if (bestGroundZ.HasValue)
            {
                XYZ linkGroundPoint = new XYZ(linkPoint.X, linkPoint.Y, bestGroundZ.Value);
                XYZ hostGroundPoint = linkTransform.OfPoint(linkGroundPoint);
                return hostGroundPoint.Z;
            }

            return null;
        }
    }
}
