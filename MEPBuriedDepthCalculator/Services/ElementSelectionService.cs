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

        public List<Element> GetSelectedElements(Document doc, UIDocument uidoc, CalculationOptions options)
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
                        _logger.Info("ElementSelection", $"Current Selection mode: Found {elements.Count} supported elements out of {selectedIds.Count} selected.");
                        break;

                    case SelectionMode.PickElements:
                        // Interactive pick not directly available in headless/non-interactive UI context without prompt,
                        // but can use UIDoc selection or prompt. For WPF UI, we can use Selection.PickObjects if invoked from external command.
                        _logger.Info("ElementSelection", "Pick Elements mode requested.");
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
            catch (System.Exception ex)
            {
                _logger.Error("ElementSelection", $"Error retrieving elements in mode {options.SelectionMode}", ex);
            }

            return elements;
        }

        public bool IsSupportedElement(Element elem)
        {
            if (elem == null || elem.Category == null) return false;
            string catName = elem.Category.Name;
            return catName == "Pipes" || catName == "Ducts" || catName == "Conduits" ||
                   elem.Category.Id.IntegerValue == (int)BuiltInCategory.OST_PipeCurves ||
                   elem.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DuctCurves ||
                   elem.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Conduit;
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
