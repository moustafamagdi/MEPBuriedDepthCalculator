using System;
using System.IO;
using Autodesk.DB;
using MEPBuriedDepthCalculator.Logging;

namespace MEPBuriedDepthCalculator.Services
{
    public class SharedParameterService
    {
        private readonly ILogger _logger;

        public SharedParameterService(ILogger logger)
        {
            _logger = logger;
        }

        public bool EnsureSharedParametersExist(Document doc, out string message)
        {
            try
            {
                Application app = doc.Application;
                string spFilePath = app.SharedParametersFilename;

                if (string.IsNullOrEmpty(spFilePath) || !File.Exists(spFilePath))
                {
                    message = "Revit Shared Parameter file is not configured or does not exist in Revit settings.";
                    _logger.Error("ParameterBinding", message);
                    return false;
                }

                DefinitionFile defFile = app.OpenSharedParameterFile();
                if (defFile == null)
                {
                    message = "Failed to open Revit Shared Parameter file.";
                    _logger.Error("ParameterBinding", message);
                    return false;
                }

                // Ensure group exists
                DefinitionGroup group = defFile.Groups.get_Item("MEP Buried Depth Calculator");
                if (group == null)
                {
                    group = defFile.Groups.Create("MEP Buried Depth Calculator");
                }

                string[] paramNames = new[]
                {
                    Constants.ParamStartGroundElevation,
                    Constants.ParamStartDepth,
                    Constants.ParamEndGroundElevation,
                    Constants.ParamEndDepth
                };

                BindingMap bindingMap = doc.ParameterBindings;
                CategorySet catSet = app.Create.NewCategorySet();
                catSet.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_PipeCurves));
                catSet.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_DuctCurves));
                catSet.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_Conduit));

                using (Transaction t = new Transaction(doc, "Ensure Shared Parameters"))
                {
                    t.Start();

                    foreach (string paramName in paramNames)
                    {
                        Definition def = group.Definitions.get_Item(paramName);
                        if (def == null)
                        {
                            // Create external definition for Revit 2024 (using SpecTypeId.Length)
                            var opt = new ExternalDefinitionCreationOptions(paramName, SpecTypeId.Length)
                            {
                                UserModifiable = true,
                                Description = "Calculated by MEP Buried Depth Calculator add-in."
                            };
                            def = group.Definitions.Create(opt);
                            _logger.Info("ParameterBinding", $"Created shared parameter definition: {paramName}");
                        }

                        // Check binding
                        InstanceBinding instanceBinding = app.Create.NewInstanceBinding(catSet);
                        if (!bindingMap.Contains(def))
                        {
                            bindingMap.Insert(def, instanceBinding, BuiltInParameterGroup.PG_DATA);
                            _logger.Info("ParameterBinding", $"Bound shared parameter to categories: {paramName}");
                        }
                        else
                        {
                            // Verify existing binding
                            Binding existingBinding = bindingMap.get_Item(def);
                            if (!(existingBinding is InstanceBinding))
                            {
                                _logger.Warning("ParameterBinding", $"Parameter '{paramName}' exists but is not an Instance binding.");
                            }
                        }
                    }

                    t.Commit();
                }

                message = "All shared parameters verified and bound successfully.";
                _logger.Info("ParameterBinding", message);
                return true;
            }
            catch (Exception ex)
            {
                message = $"Error ensuring shared parameters: {ex.Message}";
                _logger.Error("ParameterBinding", message, ex);
                return false;
            }
        }
    }
}
