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

## Deployment & Sharing

### Automatic Installation (Recommended)
We have provided an `Install.bat` script to handle the deployment automatically.
1. Build the project in **Release** mode.
2. Ensure `MEPBuriedDepthCalculator.addin` and the `MEPBuriedDepthCalculator` folder (containing the DLL) are in the same directory as the script.
3. Double-click **`Install.bat`**.

### Sharing with Colleagues
To share this tool, create a folder named `Hatco_Setup` containing:
1. `Install.bat`
2. `MEPBuriedDepthCalculator.addin`
3. A subfolder named `MEPBuriedDepthCalculator` containing the compiled `MEPBuriedDepthCalculator.dll`.

Zip this folder and send it. Your colleagues only need to unzip and run the `Install.bat` file.

> **Note:** The installer is optimized to copy the DLL file **first**, followed by the manifest file. This ensures that Revit only detects the new add-in once all required binary files are already in place.

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
