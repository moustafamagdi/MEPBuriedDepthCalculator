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
        private string _parameterStatusText = "Status: Checking...";
        private string _summaryText = "Ready to calculate.";
        private bool _isBusy;
        private bool _parametersVerified;

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

        public string PickedCountText => _pickedElementIds.Count > 0 ? $"({_pickedElementIds.Count} picked)" : "";

        public string CalculateButtonText => IsBusy ? "Calculating..." : "Calculate & Update Depths";

        public ICommand RefreshLinksCommand { get; }
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
            PickElementsCommand = new RelayCommand(() => _eventService.Run(PickElementsAction));
            CalculateCommand = new RelayCommand(() => _eventService.Run(CalculateAction), CanCalculate);

            // Auto-check parameters on start
            _eventService.Run(uiapp => EnsureParametersInternal(uiapp, false));
        }

        private bool CanCalculate()
        {
            if (IsBusy) return false;
            if (SelectedLink == null) return false;
            if (IsPickElements && _pickedElementIds.Count == 0) return false;
            
            // For current selection, check if anything is selected
            if (IsCurrentSelection && _uidoc.Selection.GetElementIds().Count == 0) return false;

            return true;
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
            ElementId previousInstanceId = _selectedLink?.InstanceId;
            AvailableLinks.Clear();
            var linkService = new LinkedModelService(_logger);
            var links = linkService.GetRevitLinks(_doc);
            foreach (var l in links) AvailableLinks.Add(l);

            var rematch = previousInstanceId != null ? AvailableLinks.FirstOrDefault(l => l.InstanceId.Value == previousInstanceId.Value) : null;
            SelectedLink = rematch ?? (AvailableLinks.Count > 0 ? AvailableLinks[0] : null);
        }

        private bool EnsureParametersInternal(UIApplication uiapp, bool showDialogOnSuccess)
        {
            var doc = uiapp.ActiveUIDocument.Document;
            var paramService = new SharedParameterService(_logger);
            if (paramService.EnsureSharedParametersExist(doc, out string msg))
            {
                ParameterStatusText = "Status: Verified & Bound";
                _parametersVerified = true;
                if (showDialogOnSuccess) TaskDialog.Show(Constants.AddInName, "Shared parameters are ready.");
                return true;
            }
            else
            {
                ParameterStatusText = "Status: Error / Missing";
                _parametersVerified = false;
                TaskDialog.Show(Constants.AddInName, msg);
                return false;
            }
        }

        private void PickElementsAction(UIApplication uiapp)
        {
            try
            {
                var uidoc = uiapp.ActiveUIDocument;
                var selectionService = new ElementSelectionService(_logger);
                var pickedRefs = uidoc.Selection.PickObjects(ObjectType.Element, "Select MEP elements (Pipes, Ducts, Conduits)");
                
                _pickedElementIds.Clear();
                foreach (var reference in pickedRefs)
                {
                    var elem = uidoc.Document.GetElement(reference);
                    if (selectionService.IsSupportedElement(elem)) _pickedElementIds.Add(elem.Id);
                }

                IsPickElements = true;
                OnPropertyChanged(nameof(PickedCountText));
                SummaryText = $"Manually picked {_pickedElementIds.Count} elements.";
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
            catch (Exception ex) { _logger.Error("Selection", "Error picking elements", ex); }
        }

        private void CalculateAction(UIApplication uiapp)
        {
            var uidoc = uiapp.ActiveUIDocument;
            var doc = uidoc.Document;

            if (!_parametersVerified && !EnsureParametersInternal(uiapp, false)) return;

            IsBusy = true;
            SummaryText = "Calculating... please wait.";
            var oldCursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = Cursors.Wait;
            _window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            try
            {
                var options = new CalculationOptions { SelectedLinkInstanceId = SelectedLink.InstanceId, SelectedLinkName = SelectedLink.Name };
                if (IsCurrentSelection) options.SelectionMode = SelectionMode.CurrentSelection;
                else if (IsPickElements) options.SelectionMode = SelectionMode.PickElements;
                else if (IsCurrentView) options.SelectionMode = SelectionMode.CurrentView;
                else if (IsEntireModel) options.SelectionMode = SelectionMode.EntireModel;

                var selectionService = new ElementSelectionService(_logger);
                var elements = selectionService.GetSelectedElements(doc, uidoc, options, _pickedElementIds);

                if (elements.Count == 0)
                {
                    TaskDialog.Show(Constants.AddInName, "No supported elements found.");
                    return;
                }

                var calcService = new DepthCalculationService(_logger);
                var results = calcService.CalculateAndApply(doc, elements, options, out CalculationSummary summary);

                ResultRows.Clear();
                foreach (var res in results)
                {
                    ResultRows.Add(new ElementResultRow {
                        ElementIdText = res.ElementId.Value.ToString(),
                        CategoryName = res.CategoryName,
                        StatusText = res.Status.ToString(),
                        StartDepthText = res.StartResult?.IsValid == true ? res.StartResult.Depth.ToString("F2") : "N/A",
                        EndDepthText = res.EndResult?.IsValid == true ? res.EndResult.Depth.ToString("F2") : "N/A",
                        NotesText = string.Join(", ", res.Errors)
                    });
                }

                string msg = $"Complete! Updated: {summary.Updated}, Errors: {summary.Errors}";
                SummaryText = msg;
                TaskDialog.Show(Constants.AddInName, msg);
            }
            finally
            {
                IsBusy = false;
                Mouse.OverrideCursor = oldCursor;
            }
        }

        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
