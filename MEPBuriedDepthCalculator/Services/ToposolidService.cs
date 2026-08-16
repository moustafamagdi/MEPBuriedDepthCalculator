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

    /// <summary>
    /// A single triangle from a Toposolid's triangulated top surface, in the LINKED document's
    /// local coordinate system, with a precomputed 2D bounding box for fast rejection.
    /// </summary>
    public class ToposolidTriangle
    {
        public XYZ P1, P2, P3;
        public double MinX, MaxX, MinY, MaxY;
        public ElementId ToposolidId;
    }

    /// <summary>
    /// Pre-triangulated Toposolid geometry for one linked document, built once per calculation
    /// run and reused for every element/endpoint query. Triangulating on every lookup (the
    /// previous behavior) is the dominant cost on large models — this cache removes that.
    /// </summary>
    public class ToposolidGeometryCache
    {
        public List<ToposolidTriangle> Triangles { get; } = new List<ToposolidTriangle>();
        public int ToposolidCount { get; set; }
    }

    public class ToposolidService
    {
        private readonly ILogger _logger;

        public ToposolidService(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Collects every Toposolid in the linked document and triangulates it exactly once.
        /// Call this a single time per calculation run (per selected link), then reuse the
        /// result for every element endpoint via FindNearestUpperGround(cache, ...).
        /// </summary>
        public ToposolidGeometryCache BuildCache(Document linkedDoc)
        {
            var cache = new ToposolidGeometryCache();
            try
            {
                var options = new Options
                {
                    ComputeReferences = false,
                    DetailLevel = ViewDetailLevel.Fine
                };

                var collector = new FilteredElementCollector(linkedDoc)
                    .WhereElementIsNotElementType();

                foreach (Element elem in collector)
                {
                    if (elem.Category == null) continue;
                    bool isToposolid = elem.Category.Id.Value == (long)BuiltInCategory.OST_Toposolid
                                        || elem.Category.Name.Contains("Toposolid");
                    if (!isToposolid) continue;

                    cache.ToposolidCount++;

                    GeometryElement geomElem = elem.get_Geometry(options);
                    if (geomElem == null) continue;

                    foreach (GeometryObject geomObj in geomElem)
                    {
                        if (!(geomObj is Solid solid) || solid.Faces.Size == 0) continue;

                        foreach (Face face in solid.Faces)
                        {
                            Mesh mesh;
                            try { mesh = face.Triangulate(); }
                            catch { continue; }
                            if (mesh == null) continue;

                            for (int i = 0; i < mesh.NumTriangles; i++)
                            {
                                MeshTriangle mt = mesh.get_Triangle(i);
                                XYZ p1 = mt.get_Vertex(0);
                                XYZ p2 = mt.get_Vertex(1);
                                XYZ p3 = mt.get_Vertex(2);

                                cache.Triangles.Add(new ToposolidTriangle
                                {
                                    P1 = p1,
                                    P2 = p2,
                                    P3 = p3,
                                    MinX = Math.Min(p1.X, Math.Min(p2.X, p3.X)),
                                    MaxX = Math.Max(p1.X, Math.Max(p2.X, p3.X)),
                                    MinY = Math.Min(p1.Y, Math.Min(p2.Y, p3.Y)),
                                    MaxY = Math.Max(p1.Y, Math.Max(p2.Y, p3.Y)),
                                    ToposolidId = elem.Id
                                });
                            }
                        }
                    }
                }

                _logger.Info("ToposolidGeometry", $"Cache built: {cache.ToposolidCount} Toposolid(s), {cache.Triangles.Count} triangles.");
            }
            catch (Exception ex)
            {
                _logger.Error("ToposolidGeometry", "Error building Toposolid geometry cache", ex);
            }

            return cache;
        }

        /// <summary>
        /// Finds the nearest upper ground surface for a host-space point using a pre-built
        /// triangle cache (see BuildCache). No collection or triangulation happens here.
        /// </summary>
        public GroundCandidate FindNearestUpperGround(ToposolidGeometryCache cache, Transform linkTransform, XYZ hostPoint, double elementBottomZ)
        {
            if (cache == null || cache.Triangles.Count == 0)
            {
                return null;
            }

            try
            {
                Transform inverseTransform = linkTransform.Inverse;
                XYZ linkPoint = inverseTransform.OfPoint(hostPoint);

                // Per-Toposolid highest local Z at this X/Y (mirrors the original per-element
                // EvaluateToposolidAtPoint behavior: take the top-most triangle hit belonging
                // to each individual Toposolid, since one solid can contribute several faces).
                var maxLocalZByToposolid = new Dictionary<long, double>();

                foreach (var tri in cache.Triangles)
                {
                    // Fast 2D bounding-box rejection before the more expensive barycentric test.
                    if (linkPoint.X < tri.MinX || linkPoint.X > tri.MaxX ||
                        linkPoint.Y < tri.MinY || linkPoint.Y > tri.MaxY)
                    {
                        continue;
                    }

                    if (!GeometryUtils.IsPointInsideTriangle(linkPoint, tri.P1, tri.P2, tri.P3)) continue;

                    double? z = GeometryUtils.InterpolateZOnTriangle(linkPoint, tri.P1, tri.P2, tri.P3);
                    if (!z.HasValue) continue;

                    long key = tri.ToposolidId.Value;
                    if (!maxLocalZByToposolid.TryGetValue(key, out double existingZ) || z.Value > existingZ)
                    {
                        maxLocalZByToposolid[key] = z.Value;
                    }
                }

                if (maxLocalZByToposolid.Count == 0)
                {
                    return null;
                }

                // Transform each Toposolid's surface point back to host coordinates and keep
                // only surfaces at/above the element's bottom, then pick the NEAREST one above
                // it (minimum qualifying elevation) — matches the "nearest upper surface" spec.
                var qualifying = new List<GroundCandidate>();
                foreach (var kvp in maxLocalZByToposolid)
                {
                    XYZ linkGroundPoint = new XYZ(linkPoint.X, linkPoint.Y, kvp.Value);
                    XYZ hostGroundPoint = linkTransform.OfPoint(linkGroundPoint);

                    if (hostGroundPoint.Z >= elementBottomZ - 1e-5)
                    {
                        qualifying.Add(new GroundCandidate
                        {
                            ToposolidId = new ElementId(kvp.Key),
                            GroundElevation = hostGroundPoint.Z
                        });
                    }
                }

                if (qualifying.Count == 0)
                {
                    return null;
                }

                qualifying.Sort((a, b) => a.GroundElevation.CompareTo(b.GroundElevation));
                var selected = qualifying[0];

                if (qualifying.Count > 1)
                {
                    _logger.Warning("ToposolidDiscovery", $"Multiple ground surfaces ({qualifying.Count}) detected above element point. Nearest upper surface (ID: {selected.ToposolidId}, Elev: {selected.GroundElevation:F4}) selected.");
                }

                return selected;
            }
            catch (Exception ex)
            {
                _logger.Error("ToposolidGeometry", "Error evaluating Toposolid surface elevation", ex);
                return null;
            }
        }
    }
}
