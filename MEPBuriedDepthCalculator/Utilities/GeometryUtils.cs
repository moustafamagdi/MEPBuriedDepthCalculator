using System;
using Autodesk.Revit.DB;

namespace MEPBuriedDepthCalculator.Utilities
{
    public static class GeometryUtils
    {
        public static bool IsVertical(XYZ start, XYZ end, double tolerance = Constants.VerticalToleranceFeet)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double horizontalDistance = Math.Sqrt(dx * dx + dy * dy);
            return horizontalDistance < tolerance;
        }

        public static bool IsPointInsideTriangle(XYZ p, XYZ a, XYZ b, XYZ c)
        {
            // Using barycentric coordinates in X-Y plane for Toposolid triangulation projection
            double x = p.X, y = p.Y;
            double x1 = a.X, y1 = a.Y;
            double x2 = b.X, y2 = b.Y;
            double x3 = c.X, y3 = c.Y;

            double denominator = ((y2 - y3) * (x1 - x3) + (x3 - x2) * (y1 - y3));
            if (Math.Abs(denominator) < 1e-12) return false;

            double a_coord = ((y2 - y3) * (x - x3) + (x3 - x2) * (y - y3)) / denominator;
            double b_coord = ((y3 - y1) * (x - x3) + (x1 - x3) * (y - y3)) / denominator;
            double c_coord = 1.0 - a_coord - b_coord;

            double tolerance = -1e-6;
            return a_coord >= tolerance && b_coord >= tolerance && c_coord >= tolerance;
        }

        public static double? InterpolateZOnTriangle(XYZ p, XYZ a, XYZ b, XYZ c)
        {
            double x = p.X, y = p.Y;
            // Barycentric interpolation
            double det = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
            if (Math.Abs(det) < 1e-12) return null;

            double l1 = ((b.Y - c.Y) * (x - c.X) + (c.X - b.X) * (y - c.Y)) / det;
            double l2 = ((c.Y - a.Y) * (x - c.X) + (a.X - c.X) * (y - c.Y)) / det;
            double l3 = 1.0 - l1 - l2;

            if (l1 >= -1e-6 && l2 >= -1e-6 && l3 >= -1e-6)
            {
                return l1 * a.Z + l2 * b.Z + l3 * c.Z;
            }
            return null;
        }
    }
}
