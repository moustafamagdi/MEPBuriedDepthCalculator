# Hatco MEP Buried Depth Calculator for Revit 2024

An enterprise-grade Autodesk Revit 2024 C# add-in designed to calculate the vertical buried depth of MEP elements (Pipes, Ducts, and Conduits) below Finished Ground surfaces represented by Toposolids inside Linked Revit models.

---

## Key Features

* **Hatco Ribbon Tab**: Integrated into a custom "Hatco" ribbon tab for easy access.
* **Revit 2024 Compatibility**: Built for Revit 2024 (.NET Framework 4.8).
* **Supported Categories**: Pipes (`OST_PipeCurves`), Ducts (`OST_DuctCurves`), and Conduits (`OST_Conduit`).
* **Start & End Calculations**: Independently calculates start and end ground elevations, bottom elevations, and buried depths based on global Z coordinates.
* **Physical Bottom Elevation**: Accounts for element dimensions (pipe/conduit radius or duct height/diameter) to compute distance to the true physical bottom of the element.
* **Revit Link Integration**: Supports explicit selection of linked Revit models containing Finished Ground Toposolids, correctly transforming host endpoint coordinates via shared coordinates and link transforms.
* **Optimized Performance**: Uses geometric caching and triangulation indexing to handle large site models efficiently.
* **Shared Parameters**: Automatically verifies, creates, and binds four instance Length parameters (`Start Ground Elevation`, `Start Depth`, `End Ground Elevation`, `End Depth`) without duplication.
* **Robust Diagnostic Logging**: Generates timestamped diagnostic log files on the user's Desktop for remote debugging and traceability.
* **Modeless WPF Interface**: Provides a professional, non-intrusive user interface that stays open while interacting with Revit.

---

## Solution Architecture

```
MEPBuriedDepthCalculator
│
├── App.cs (Hatco Ribbon UI Setup)
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
│   ├── BottomElevationService.cs
│   ├── ToposolidService.cs (Geometric Caching)
│   ├── LinkedModelService.cs
│   ├── DepthCalculationService.cs
│   ├── SharedParameterService.cs
│   ├── RevitEventService.cs (ExternalEvent handling)
│   └── SettingsService.cs (User persistence)
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
    └── GeometryUtils.cs
```

---

## Deliverables & Documentation

* **Source Code**: Complete C# and XAML implementation.
* **Visual Studio Solution**: `MEPBuriedDepthCalculator.sln` (.NET Framework 4.8).
* **Add-in Manifest**: `MEPBuriedDepthCalculator.addin`.
* **Installation Guide**: Refer to `INSTALLATION.md`.
* **Logs**: Diagnostic logs are saved to your Desktop with the prefix `Hatco_MEPBuriedDepthCalculator_*.log`.
