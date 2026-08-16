using System;
using Autodesk.Revit.DB;
using MEPBuriedDepthCalculator.Models;

namespace MEPBuriedDepthCalculator.Utilities
{
    public static class UnitConversionService
    {
        private const double FeetToMeters = 0.3048;
        private const double MetersToFeet = 1.0 / FeetToMeters;
        private const double FeetToMillimeters = 304.8;
        private const double MillimetersToFeet = 1.0 / FeetToMillimeters;
        private const double FeetToCentimeters = 30.48;
        private const double CentimetersToFeet = 1.0 / FeetToCentimeters;
        private const double FeetToInches = 12.0;
        private const double InchesToFeet = 1.0 / 12.0;

        public static double ConvertFromInternalFeet(double valueInFeet, DisplayUnitTypeOption unitOption, Document doc)
        {
            switch (unitOption)
            {
                case DisplayUnitTypeOption.Millimeters:
                    return valueInFeet * FeetToMillimeters;
                case DisplayUnitTypeOption.Centimeters:
                    return valueInFeet * FeetToCentimeters;
                case DisplayUnitTypeOption.Meters:
                    return valueInFeet * FeetToMeters;
                case DisplayUnitTypeOption.Inches:
                    return valueInFeet * FeetToInches;
                case DisplayUnitTypeOption.Feet:
                    return valueInFeet;
                case DisplayUnitTypeOption.ProjectUnits:
                default:
                    // In Revit 2024, UnitUtils can be used, or fallback to feet/meters depending on project settings.
                    // For general UI display of length, we can use UnitUtils if available or default to feet / meters.
                    try
                    {
                        #if REVIT2024
                        ForgeTypeId displayUnit = doc.GetUnits().GetFormatOptions(SpecTypeId.Length).GetUnitTypeId();
                        return UnitUtils.ConvertFromInternalUnits(valueInFeet, displayUnit);
                        #else
                        return valueInFeet; // default internal feet
                        #endif
                    }
                    catch
                    {
                        return valueInFeet;
                    }
            }
        }

        public static string FormatValue(double valueInFeet, DisplayUnitTypeOption unitOption, Document doc)
        {
            double converted = ConvertFromInternalFeet(valueInFeet, unitOption, doc);
            return converted.ToString("F2");
        }

        public static string GetUnitLabel(DisplayUnitTypeOption unitOption)
        {
            switch (unitOption)
            {
                case DisplayUnitTypeOption.Millimeters: return "mm";
                case DisplayUnitTypeOption.Centimeters: return "cm";
                case DisplayUnitTypeOption.Meters: return "m";
                case DisplayUnitTypeOption.Feet: return "ft";
                case DisplayUnitTypeOption.Inches: return "in";
                case DisplayUnitTypeOption.ProjectUnits:
                default: return "ft (Project)";
            }
        }
    }
}
