using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MEPBuriedDepthCalculator.Models;
using MEPBuriedDepthCalculator.Logging;

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
            var elements = new List<Element>();
            try
            {
                switch (options.SelectionMode)
                {
                    case SelectionMode.CurrentSelection:
                        var selectedIds = uidoc.Selection.GetElementIds();
                        foreach (var id in selectedIds)
                        {
                            var elem = doc.GetElement(id);
                            if (IsSupportedElement(elem))
                            {
                                elements.Add(elem);
                            }
                        }
                        _logger.Info("ElementSelection", $"Current Selection mode: Found {elements.Count} supported elements.");
                        break;

                    case SelectionMode.PickElements:
                        if (manualPickedIds != null && manualPickedIds.Count > 0)
                        {
                            foreach (var id in manualPickedIds)
                            {
                                var elem = doc.GetElement(id);
                                if (IsSupportedElement(elem))
                                {
                                    elements.Add(elem);
                                }
                            }
                        }
                        _logger.Info("ElementSelection", $"Pick Elements mode: Found {elements.Count} supported elements.");
                        break;

                    case SelectionMode.CurrentView:
                        var activeView = doc.ActiveView;
                        if (activeView != null)
                        {
                            var collector = new FilteredElementCollector(doc, activeView.Id)
                                .WherePasses(GetSupportedCategoriesFilter())
                                .WhereElementIsNotElementType();
                            elements.AddRange(collector.ToElements());
                        }
                        _logger.Info("ElementSelection", $"Current View mode: Found {elements.Count} supported elements.");
                        break;

                    case SelectionMode.EntireModel:
                        var modelCollector = new FilteredElementCollector(doc)
                                .WherePasses(GetSupportedCategoriesFilter())
                                .WhereElementIsNotElementType();
                        elements.AddRange(modelCollector.ToElements());
                        _logger.Info("ElementSelection", $"Entire Model mode: Found {elements.Count} supported elements.");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("ElementSelection", $"Error retrieving elements in mode {options.SelectionMode}", ex);
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
            var filters = new List<ElementFilter>
            {
                new ElementCategoryFilter(BuiltInCategory.OST_PipeCurves),
                new ElementCategoryFilter(BuiltInCategory.OST_DuctCurves),
                new ElementCategoryFilter(BuiltInCategory.OST_Conduit)
            };
            return new LogicalOrFilter(filters);
        }
    }
}
