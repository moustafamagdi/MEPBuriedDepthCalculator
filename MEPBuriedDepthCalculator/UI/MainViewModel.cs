using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using MEPBuriedDepthCalculator.Logging;
using MEPBuriedDepthCalculator.Models;
using MEPBuriedDepthCalculator.Services;

namespace MEPBuriedDepthCalculator.UI
{
    /// <summary>Display row for the Per-Element Results grid — pre-formatted for binding.</summary>
    public class ElementResultRow
    {
        public string ElementIdText { get; set; }
        public string CategoryName { get; set; }
        public string StatusText { get; set; }
        public string StartDepthText { get; set; }
        public string EndDepthText { get; set; }
        public string NotesText { get; set; }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();

        public void Execute(object parameter) => _execute();
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly Document _doc;
        private readonly UIDocument _uidoc;
        private readonly ILogger _logger;
        private readonly Window _window;
        private readonly RevitEventService _eventService;

        private bool _isCurrentSelection = true;
        private bool _isPickElements;
        private bool _isCurrentView;
        private bool _isEntireModel;

        private List<ElementId> _pickedElementIds = new List<ElementId>();
        private LinkedModelInfo _selectedLink;
        private string _parameterStatusText = "Status: Not Verified";
        private string _summaryText = "Ready to calculate.";
        private bool _parametersVerified;
        private bool _isBusy;

        public ObservableCollection<LinkedModelInfo> AvailableLinks { get; set; } = new ObservableCollection<LinkedModelInfo>();
        public ObservableCollection<ElementResultRow> ResultRows { get; set; } = new ObservableCollection<ElementResultRow>();

        public event PropertyChangedEventHandler PropertyChanged;

        public bool IsCurrentSelection
        {
            get => _isCurrentSelection;
            set { _isCurrentSelection = value; OnPropertyChanged(nameof(IsCurrentSelection)); SaveSettings(); }
        }

        public bool IsPickElements
        {
            get => _isPickElements;
            set { _isPickElements = value; OnPropertyChanged(nameof(IsPickElements)); SaveSettings(); }
        }

        public bool IsCurrentView
        {
            get => _isCurrentView;
            set { _isCurrentView = value; OnPropertyChanged(nameof(IsCurrentView)); SaveSettings(); }
        }

        public bool IsEntireModel
        {
            get => _isEntireModel;
            set { _isEntireModel = value; OnPropertyChanged(nameof(IsEntireModel)); SaveSettings(); }
        }

        public LinkedModelInfo SelectedLink
        {
            get => _selectedLink;
            set { _selectedLink = value; OnPropertyChanged(nameof(SelectedLink)); SaveSettings(); }
        }

        public string ParameterStatusText
        {
            get => _parameterStatusText;
            set { _parameterStatusText = value; OnPropertyChanged(nameof(ParameterStatusText)); }
        }

        public string SummaryText
        {
            get => _summaryText;
            set { _summaryText = value; OnPropertyChanged(nameof(SummaryText)); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(nameof(IsBusy)); OnPropertyChanged(nameof(CalculateButtonText)); }
        }

        public string CalculateButtonText => IsBusy ? "Calculating..." : "Calculate & Update Depths";

        public ICommand RefreshLinksCommand { get; }
        public ICommand EnsureParametersCommand { get; }
        public ICommand PickElementsCommand { get; }
        public ICommand CalculateCommand { get; }

        public MainViewModel(Document doc, UIDocument uidoc, ILogger logger, Window window)
        {
            _doc = doc;
            _uidoc = uidoc;
            _logger = logger;
            _window = window;
            _eventService = new RevitEventService();

            RefreshLinks();
            LoadSettings();

            RefreshLinksCommand = new RelayCommand(RefreshLinks);
            EnsureParametersCommand = new RelayCommand(() => _eventService.Run(EnsureParametersAction));
            PickElementsCommand = new RelayCommand(() => _eventService.Run(PickElementsAction));
            CalculateCommand = new RelayCommand(() => _eventService.Run(CalculateAction));
        }

        private void LoadSettings()
        {
            var settings = SettingsService.Load();
            
            IsCurrentSelection = settings.LastSelectionMode == SelectionMode.CurrentSelection;
            IsPickElements = settings.LastSelectionMode == SelectionMode.PickElements;
            IsCurrentView = settings.LastSelectionMode == SelectionMode.CurrentView;
            IsEntireModel = settings.LastSelectionMode == SelectionMode.EntireModel;

            if (!string.IsNullOrEmpty(settings.LastSelectedLinkName))
            {
                var link = AvailableLinks.FirstOrDefault(l => l.Name == settings.LastSelectedLinkName);
                if (link != null) SelectedLink = link;
            }
        }

        private void SaveSettings()
        {
            var settings = new UserSettings();
            if (IsCurrentSelection) settings.LastSelectionMode = SelectionMode.CurrentSelection;
            else if (IsPickElements) settings.LastSelectionMode = SelectionMode.PickElements;
            else if (IsCurrentView) settings.LastSelectionMode = SelectionMode.CurrentView;
            else if (IsEntireModel) settings.LastSelectionMode = SelectionMode.EntireModel;

            if (SelectedLink != null)
            {
                settings.LastSelectedLinkName = SelectedLink.Name;
                settings.LastSelectedLinkInstanceId = SelectedLink.InstanceId.Value;
            }

            SettingsService.Save(settings);
        }

        private void RefreshLinks()
        {
            // Remember the previously selected link's InstanceId (not the object reference,
            // which becomes stale/orphaned the moment AvailableLinks is rebuilt below).
            ElementId previousInstanceId = _selectedLink?.InstanceId;

            AvailableLinks.Clear();
            var linkService = new LinkedModelService(_logger);
            var links = linkService.GetRevitLinks(_doc);
            foreach (var l in links)
            {
                AvailableLinks.Add(l);
            }

            // Re-select by matching InstanceId against the new collection, since the old
            // LinkedModelInfo instance is no longer reference-equal to anything in the list.
            var rematch = previousInstanceId != null
                ? AvailableLinks.FirstOrDefault(l => l.InstanceId == previousInstanceId)
                : null;

            if (rematch != null)
            {
                SelectedLink = rematch;
            }
            else if (AvailableLinks.Count > 0)
            {
                SelectedLink = AvailableLinks[0];
            }
            else
            {
                SelectedLink = null;
            }

            _logger.Info("LinkSelection", $"Refreshed links list. Found {AvailableLinks.Count} links.");
        }

        private void EnsureParametersAction(UIApplication uiapp)
        {
            EnsureParametersInternal(uiapp, showDialogOnSuccess: true);
        }

        /// <summary>
        /// Verifies/binds the shared parameters. Returns true on success. Used both by the
        /// explicit "Ensure Parameters" button and automatically at the start of Calculate,
        /// so a user who forgets to click it first doesn't silently get unwritten parameters.
        /// </summary>
        private bool EnsureParametersInternal(UIApplication uiapp, bool showDialogOnSuccess)
        {
            var doc = uiapp.ActiveUIDocument.Document;
            var paramService = new SharedParameterService(_logger);
            if (paramService.EnsureSharedParametersExist(doc, out string msg))
            {
                ParameterStatusText = "Status: Verified & Bound";
                _parametersVerified = true;
                SummaryText = "Shared parameters successfully verified.";
                if (showDialogOnSuccess)
                {
                    TaskDialog.Show(Constants.AddInName, "Shared parameters are ready.");
                }
                return true;
            }
            else
            {
                ParameterStatusText = "Status: Error / Missing";
                _parametersVerified = false;
                SummaryText = msg;
                TaskDialog.Show(Constants.AddInName, msg);
                return false;
            }
        }

        private void PickElementsAction(UIApplication uiapp)
        {
            try
            {
                var uidoc = uiapp.ActiveUIDocument;
                var doc = uidoc.Document;
                
                // Note: Using modeless-like behavior with Raise() usually works best with Show()
                // But if we are in ShowDialog, we need to be careful.
                // For simplicity in modal dialog, we just execute directly.
                
                var selectionService = new ElementSelectionService(_logger);
                var pickedRefs = uidoc.Selection.PickObjects(ObjectType.Element, "Select MEP elements (Pipes, Ducts, Conduits)");
                
                _pickedElementIds.Clear();
                foreach (var reference in pickedRefs)
                {
                    var elem = doc.GetElement(reference);
                    if (selectionService.IsSupportedElement(elem))
                    {
                        _pickedElementIds.Add(elem.Id);
                    }
                }

                IsPickElements = true;
                SummaryText = $"Manually picked {_pickedElementIds.Count} supported elements.";
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                SummaryText = "Picking canceled.";
            }
            catch (Exception ex)
            {
                _logger.Error("Selection", "Error picking elements", ex);
            }
        }

        private void CalculateAction(UIApplication uiapp)
        {
            var uidoc = uiapp.ActiveUIDocument;
            var doc = uidoc.Document;

            if (SelectedLink == null)
            {
                SummaryText = "Error: Please select a Finished Ground Revit Link.";
                TaskDialog.Show(Constants.AddInName, "Please select a Revit Link first.");
                return;
            }

            // A user picking "Manually Picked" but never clicking "Pick Elements from Revit"
            // used to silently fall through to an empty selection with a generic error.
            // Catch it here with a message that tells them what to actually do.
            if (IsPickElements && _pickedElementIds.Count == 0)
            {
                SummaryText = "No elements picked yet. Click \"Pick Elements from Revit\" first.";
                TaskDialog.Show(Constants.AddInName, "You've selected \"Manually Picked\" mode but haven't picked any elements yet.\n\nClick \"Pick Elements from Revit\" first, then Calculate.");
                return;
            }

            // Auto-verify/bind shared parameters if the user hasn't already done so via the
            // explicit button — previously, skipping that step meant parameter writes would
            // silently fail later with only a log entry to show for it.
            if (!_parametersVerified)
            {
                if (!EnsureParametersInternal(uiapp, showDialogOnSuccess: false))
                {
                    // EnsureParametersInternal already showed the error dialog.
                    return;
                }
            }

            var options = new CalculationOptions
            {
                SelectedLinkInstanceId = SelectedLink.InstanceId,
                SelectedLinkName = SelectedLink.Name
            };

            if (IsCurrentSelection) options.SelectionMode = SelectionMode.CurrentSelection;
            else if (IsPickElements) options.SelectionMode = SelectionMode.PickElements;
            else if (IsCurrentView) options.SelectionMode = SelectionMode.CurrentView;
            else if (IsEntireModel) options.SelectionMode = SelectionMode.EntireModel;

            List<Element> elements;
            if (IsPickElements && _pickedElementIds.Count > 0)
            {
                elements = _pickedElementIds.Select(id => doc.GetElement(id)).ToList();
            }
            else
            {
                var selectionService = new ElementSelectionService(_logger);
                elements = selectionService.GetSelectedElements(doc, uidoc, options);
            }

            if (elements == null || elements.Count == 0)
            {
                SummaryText = "Warning: No supported MEP elements found.";
                TaskDialog.Show(Constants.AddInName, "No supported MEP elements found for processing.");
                return;
            }

            // Set busy state and force a Dispatcher render pass BEFORE the blocking calculation
            // runs. Without this, the window (running on Revit's single UI thread) never repaints
            // the "Calculating..." message or the wait cursor — it just appears to freeze.
            IsBusy = true;
            SummaryText = $"Calculating {elements.Count} element(s)... please wait.";
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            _window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            try
            {
                var calcService = new DepthCalculationService(_logger);
                var results = calcService.CalculateAndApply(doc, elements, options, out CalculationSummary summary);

                PopulateResultRows(doc, results);

                string resultMsg = $"Calculation Complete!\n\n" +
                                   $"Total Selected: {summary.TotalSelected}\n" +
                                   $"Processed: {summary.Processed}\n" +
                                   $"Updated: {summary.Updated}\n" +
                                   $"Skipped: {summary.Skipped}\n" +
                                   $"Errors: {summary.Errors}\n" +
                                   $"Duration: {summary.Duration.TotalSeconds:F2}s\n\n" +
                                   $"Detailed log saved to your Desktop.";

                SummaryText = resultMsg;
                TaskDialog.Show(Constants.AddInName, resultMsg);
            }
            finally
            {
                System.Windows.Input.Mouse.OverrideCursor = null;
                IsBusy = false;
            }
        }

        private void PopulateResultRows(Document doc, List<Models.ElementCalculationResult> results)
        {
            ResultRows.Clear();
            foreach (var r in results)
            {
                var notes = new List<string>();
                if (r.Warnings != null) notes.AddRange(r.Warnings);
                if (r.Errors != null) notes.AddRange(r.Errors);
                if (r.StartResult?.Warning != null) notes.Add(r.StartResult.Warning);
                if (r.StartResult?.Error != null) notes.Add(r.StartResult.Error);
                if (r.EndResult?.Warning != null) notes.Add(r.EndResult.Warning);
                if (r.EndResult?.Error != null) notes.Add(r.EndResult.Error);

                ResultRows.Add(new ElementResultRow
                {
                    ElementIdText = r.ElementId?.ToString() ?? "-",
                    CategoryName = r.CategoryName,
                    StatusText = r.Status.ToString(),
                    StartDepthText = r.StartResult != null && r.StartResult.IsValid ? FormatLength(doc, r.StartResult.Depth) : "-",
                    EndDepthText = r.EndResult != null && r.EndResult.IsValid ? FormatLength(doc, r.EndResult.Depth) : "-",
                    NotesText = string.Join("; ", notes.Distinct())
                });
            }
        }

        /// <summary>Formats a length stored in Revit's internal feet using the document's
        /// actual project display units, instead of showing raw internal-foot values.</summary>
        private string FormatLength(Document doc, double internalFeetValue)
        {
            try
            {
                var formatOptions = doc.GetUnits().GetFormatOptions(SpecTypeId.Length);
                double converted = UnitUtils.ConvertFromInternalUnits(internalFeetValue, formatOptions.GetUnitTypeId());
                string symbol = LabelUtils.GetLabelForUnit(formatOptions.GetUnitTypeId());
                return $"{converted:F2} {symbol}";
            }
            catch
            {
                return $"{internalFeetValue:F3} ft";
            }
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
