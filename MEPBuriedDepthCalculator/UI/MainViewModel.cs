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

        private bool _isCurrentSelection = true;
        private bool _isPickElements;
        private bool _isCurrentView;
        private bool _isEntireModel;

        private List<ElementId> _pickedElementIds = new List<ElementId>();
        private LinkedModelInfo _selectedLink;
        private string _parameterStatusText = "Status: Not Verified";
        private string _summaryText = "Ready to calculate.";

        public ObservableCollection<LinkedModelInfo> AvailableLinks { get; set; } = new ObservableCollection<LinkedModelInfo>();

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

            RefreshLinks();
            LoadSettings();

            RefreshLinksCommand = new RelayCommand(RefreshLinks);
            EnsureParametersCommand = new RelayCommand(EnsureParameters);
            PickElementsCommand = new RelayCommand(PickElements);
            CalculateCommand = new RelayCommand(Calculate);
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
            AvailableLinks.Clear();
            var linkService = new LinkedModelService(_logger);
            var links = linkService.GetRevitLinks(_doc);
            foreach (var l in links)
            {
                AvailableLinks.Add(l);
            }
            if (AvailableLinks.Count > 0 && SelectedLink == null)
            {
                SelectedLink = AvailableLinks[0];
            }
            _logger.Info("LinkSelection", $"Refreshed links list. Found {AvailableLinks.Count} links.");
        }

        private void EnsureParameters()
        {
            var paramService = new SharedParameterService(_logger);
            if (paramService.EnsureSharedParametersExist(_doc, out string msg))
            {
                ParameterStatusText = "Status: Verified & Bound";
                SummaryText = "Shared parameters successfully verified.";
                TaskDialog.Show(Constants.AddInName, "Shared parameters are ready.");
            }
            else
            {
                ParameterStatusText = "Status: Error / Missing";
                SummaryText = msg;
                TaskDialog.Show(Constants.AddInName, msg);
            }
        }

        private void PickElements()
        {
            try
            {
                _window.Hide();
                var selectionService = new ElementSelectionService(_logger);
                var pickedRefs = _uidoc.Selection.PickObjects(ObjectType.Element, "Select MEP elements (Pipes, Ducts, Conduits)");
                
                _pickedElementIds.Clear();
                foreach (var reference in pickedRefs)
                {
                    var elem = _doc.GetElement(reference);
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
            finally
            {
                _window.Show();
            }
        }

        private void Calculate()
        {
            if (SelectedLink == null)
            {
                SummaryText = "Error: Please select a Finished Ground Revit Link.";
                TaskDialog.Show(Constants.AddInName, "Please select a Revit Link first.");
                return;
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
                elements = _pickedElementIds.Select(id => _doc.GetElement(id)).ToList();
            }
            else
            {
                var selectionService = new ElementSelectionService(_logger);
                elements = selectionService.GetSelectedElements(_doc, _uidoc, options);
            }

            if (elements == null || elements.Count == 0)
            {
                SummaryText = "Warning: No supported MEP elements found.";
                TaskDialog.Show(Constants.AddInName, "No supported MEP elements found for processing.");
                return;
            }

            SummaryText = "Calculating... please wait.";
            
            var calcService = new DepthCalculationService(_logger);
            var results = calcService.CalculateAndApply(_doc, elements, options, out CalculationSummary summary);

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

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
