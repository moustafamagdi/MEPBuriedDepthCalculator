# MEP Buried Depth Calculator for Revit 2024

An enterprise-grade Autodesk Revit 2024 C# add-in designed to calculate the vertical buried depth of MEP elements (Pipes, Ducts, and Conduits) below Finished Ground surfaces represented by Toposolids inside Linked Revit models.

---

## Key Features

* **Revit 2024 Compatibility**: Built for Revit 2024 (.NET Framework 4.8).
* **Supported Categories**: Pipes (`OST_PipeCurves`), Ducts (`OST_DuctCurves`), and Conduits (`OST_Conduit`).
* **Start & End Calculations**: Independently calculates start and end ground elevations, bottom elevations, and buried depths based on global Z coordinates.
* **Physical Bottom Elevation**: Accounts for element dimensions (pipe/conduit radius or duct height/diameter) to compute distance to the true physical bottom of the element.
* **Revit Link Integration**: Supports explicit selection of linked Revit models containing Finished Ground Toposolids, correctly transforming host endpoint coordinates via shared coordinates and link transforms.
* **Nearest Upper Surface Selection**: Automatically identifies and evaluates all candidate Toposolid surfaces directly above each endpoint, selecting the nearest upper surface and logging multiple-surface warnings when applicable.
* **Shared Parameters**: Automatically verifies, creates, and binds four instance Length parameters (`Start Ground Elevation`, `Start Depth`, `End Ground Elevation`, `End Depth`) without duplication or destructive modification.
* **Robust Diagnostic Logging**: Generates timestamped diagnostic log files on the user's Desktop for remote debugging and traceability.
* **Clean WPF Interface**: Provides a professional, non-intrusive user interface with multiple selection modes (Current Selection, Current View, Entire Model) and unit options.

---

## Solution Architecture

```
MEPBuriedDepthCalculator
│
├── Commands
│   └── LaunchCommand.cs
│
├── UI
│   ├── MainWindow.xaml
│   ├── MainWindow.xaml.cs
│   └── MainViewModel.cs
│
├── Services
│   ├── ElementSelectionService.cs
│   ├── MepElementService.cs
│   ├── BottomElevationService.cs
│   ├── ToposolidService.cs
│   ├── LinkedModelService.cs
│   ├── DepthCalculationService.cs
│   ├── SharedParameterService.cs
│   └── UnitConversionService.cs
│
├── Models
│   ├── EndpointCalculationResult.cs
│   ├── ElementCalculationResult.cs
│   └── CalculationOptions.cs
│
├── Logging
│   ├── ILogger.cs
│   ├── FileLogger.cs
│   └── LogContext.cs
│
└── Utilities
    ├── GeometryUtils.cs
    └── UnitConversionService.cs
```

---

## Deliverables & Documentation

* **Source Code**: Complete C# and XAML implementation.
* **Visual Studio Solution**: `MEPBuriedDepthCalculator.sln` (.NET Framework 4.8).
* **Add-in Manifest**: `MEPBuriedDepthCalculator.addin`.
* **Installation Guide**: Refer to `INSTALLATION.md`.
