# Installation & Deployment Guide

## Prerequisites

* Autodesk Revit 2024 installed on Windows.
* Visual Studio 2022 (or compatible .NET Framework 4.8 development environment).
* Revit 2024 API assemblies (`RevitAPI.dll` and `RevitAPIUI.dll`), typically located in `C:\Program Files\Autodesk\Revit 2024\`.

---

## Build Instructions

1. Open `MEPBuriedDepthCalculator.sln` in Visual Studio 2022.
2. Set build configuration to **Release** or **Debug**.
3. Build the solution (`Ctrl + Shift + B`).
4. The output assembly will be generated at `MEPBuriedDepthCalculator\bin\Release\MEPBuriedDepthCalculator.dll`.

---

## Deployment to Revit

To load the add-in into Revit 2024:

1. Copy the built `.dll` file and its supporting files into the Revit Add-ins folder:
   `%appdata%\Autodesk\Revit\Addins\2024\`
2. Place the `MEPBuriedDepthCalculator.addin` manifest file directly into:
   `%appdata%\Autodesk\Revit\Addins\2024\`
3. Ensure the `<Assembly>` path inside `MEPBuriedDepthCalculator.addin` correctly points to the deployed `.dll`.

---

## Usage Workflow

1. Open your host Revit model containing the MEP elements (Pipes, Ducts, Conduits) and the linked site model containing the Toposolid Finished Ground.
2. Go to the **Add-Ins** ribbon tab in Revit and click **MEP Buried Depth Calculator**.
3. In the dialog:
   * Select the element scope (Current Selection, Manually Picked, Current View, or Entire Model).
   * Use **Pick Elements from Revit** to select specific elements manually after launching the tool.
   * Select the Revit Link containing the Toposolid ground.
   * Verify and bind Shared Parameters by clicking **Ensure Parameters**.

   * Click **Calculate & Update Depths**.
4. Review the execution summary and check the generated timestamped log file on your Desktop for detailed diagnostics.
