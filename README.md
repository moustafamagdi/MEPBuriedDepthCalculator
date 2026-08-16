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
│   ├── BottomElevationService.cs
│   ├── ToposolidService.cs      (pre-triangulates & caches Toposolid geometry per run)
│   ├── LinkedModelService.cs
│   ├── DepthCalculationService.cs
│   ├── SharedParameterService.cs
│   ├── SettingsService.cs
│   └── RevitEventService.cs
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

## Recent Improvements (Audit Fixes)

* **Performance**: Toposolid geometry is now collected and triangulated once per calculation run (cached), instead of re-collecting and re-triangulating for every single element endpoint. A 2D bounding-box pre-check also skips most triangles before the exact point-in-triangle test. This is the dominant speedup for "Entire Model" runs.
* **Responsiveness**: The Calculate command now sets a wait cursor and forces a UI render pass before the blocking calculation starts, so the window no longer appears frozen with no feedback.
* **Bug fix**: Refreshing the Revit Link list (Refresh Links) no longer silently loses the current selection — the previously selected link is now re-matched by its stable InstanceId instead of a stale object reference.
* **Workflow**: Shared parameters are now automatically verified/bound at the start of Calculate if the user skipped the explicit "Ensure Parameters" step, instead of silently failing to write results.
* **UX**: Selecting "Manually Picked" without first clicking "Pick Elements from Revit" now shows a specific instruction instead of a generic "no elements found" message.
* **UI**: Added a per-element results grid (Element, Category, Status, Start/End Depth, Notes) so diagnostics are visible in the app itself, not only in the Desktop log file. Depth values are now formatted using the project's actual display units rather than raw internal feet.
* **Consistency**: Unified vendor branding (Hatco) across the ribbon tab, add-in manifest, assembly metadata, and settings folder path. Corrected this README's architecture diagram to match the actual source files.

## Deliverables & Documentation

* **Source Code**: Complete C# and XAML implementation.
* **Visual Studio Solution**: `MEPBuriedDepthCalculator.sln` (.NET Framework 4.8).
* **Add-in Manifest**: `MEPBuriedDepthCalculator.addin`.
* **Installation Guide**: Refer to `INSTALLATION.md`.
