using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MEPBuriedDepthCalculator.Logging;
using MEPBuriedDepthCalculator.Models;

namespace MEPBuriedDepthCalculator.Services
{
    public class ElementSelectionService
    {
        private readonly ILogger _logger;

        public ElementSelectionService(ILogger logger)
        {
            _logger = logger;
        }

        public List<Element> GetSelectedElements(Document doc, UIDocument uidoc, CalculationOptions options, List<ElementId> manualPickedIds = null)
        {
            List<Element> elements = new List<Element>();

            switch (options.SelectionMode)
            {
                case SelectionMode.CurrentSelection:
                    var selIds = uidoc.Selection.GetElementIds();
                    foreach (var id in selIds)
                    {
                        var elem = doc.GetElement(id);
                        if (IsSupportedElement(elem)) elements.Add(elem);
                    }
                    _logger.Info("Selection", $"Found {elements.Count} supported elements in current selection.");
                    break;

                case SelectionMode.PickElements:
                    if (manualPickedIds != null && manualPickedIds.Count > 0)
                    {
                        foreach (var id in manualPickedIds)
                        {
                            var elem = doc.GetElement(id);
                            if (IsSupportedElement(elem)) elements.Add(elem);
                        }
                    }
                    _logger.Info("Selection", $"Using {elements.Count} manually picked elements.");
                    break;

                case SelectionMode.CurrentView:
                    var viewCollector = new FilteredElementCollector(doc, doc.ActiveView.Id)
                        .WherePasses(GetSupportedCategoriesFilter())
                        .WhereElementIsNotElementType();
                    elements = viewCollector.ToList();
                    _logger.Info("Selection", $"Found {elements.Count} supported elements in current view.");
                    break;

                case SelectionMode.EntireModel:
                    var modelCollector = new FilteredElementCollector(doc)
                        .WherePasses(GetSupportedCategoriesFilter())
                        .WhereElementIsNotElementType();
                    elements = modelCollector.ToList();
                    _logger.Info("Selection", $"Found {elements.Count} supported elements in entire model.");
                    break;
            }

            return elements;
        }

        public bool IsSupportedElement(Element elem)
        {
            if (elem == null || elem.Category == null) return false;
            
            long catId = elem.Category.Id.Value;
            return catId == (long)BuiltInCategory.OST_PipeCurves ||
                   catId == (long)BuiltInCategory.OST_DuctCurves ||
                   catId == (long)BuiltInCategory.OST_Conduit;
        }

        private ElementFilter GetSupportedCategoriesFilter()
        {
            var categories = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_PipeCurves,
                BuiltInCategory.OST_DuctCurves,
                BuiltInCategory.OST_Conduit
            };

            var categoryFilters = categories.Select(c => new ElementCategoryFilter(c)).ToList();
            return new LogicalOrFilter(categoryFilters.Cast<ElementFilter>().ToList());
        }
    }
}
