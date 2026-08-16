using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
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

        private bool _isCurrentSelection = true;
        private bool _isCurrentView;
        private bool _isEntireModel;

        private LinkedModelInfo _selectedLink;
        private string _parameterStatusText = "Status: Not Verified";
        private string _summaryText = "Ready to calculate.";

        public ObservableCollection<LinkedModelInfo> AvailableLinks { get; set; } = new ObservableCollection<LinkedModelInfo>();
        public ObservableCollection<DisplayUnitTypeOption> AvailableUnits { get; set; } = new ObservableCollection<DisplayUnitTypeOption>();

        private DisplayUnitTypeOption _selectedUnit = DisplayUnitTypeOption.ProjectUnits;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool IsCurrentSelection
        {
            get => _isCurrentSelection;
            set { _isCurrentSelection = value; OnPropertyChanged(nameof(IsCurrentSelection)); }
        }

        public bool IsCurrentView
        {
            get => _isCurrentView;
            set { _isCurrentView = value; OnPropertyChanged(nameof(IsCurrentView)); }
        }

        public bool IsEntireModel
        {
            get => _isEntireModel;
            set { _isEntireModel = value; OnPropertyChanged(nameof(IsEntireModel)); }
        }

        public LinkedModelInfo SelectedLink
        {
            get => _selectedLink;
            set { _selectedLink = value; OnPropertyChanged(nameof(SelectedLink)); }
        }

        public DisplayUnitTypeOption SelectedUnit
        {
            get => _selectedUnit;
            set { _selectedUnit = value; OnPropertyChanged(nameof(SelectedUnit)); }
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
        public ICommand CalculateCommand { get; }

        public MainViewModel(Document doc, UIDocument uidoc, ILogger logger)
        {
            _doc = doc;
            _uidoc = uidoc;
            _logger = logger;

            // Load units
            AvailableUnits.Add(DisplayUnitTypeOption.ProjectUnits);
            AvailableUnits.Add(DisplayUnitTypeOption.Millimeters);
            AvailableUnits.Add(DisplayUnitTypeOption.Centimeters);
            AvailableUnits.Add(DisplayUnitTypeOption.Meters);
            AvailableUnits.Add(DisplayUnitTypeOption.Feet);
            AvailableUnits.Add(DisplayUnitTypeOption.Inches);

            RefreshLinks();

            RefreshLinksCommand = new RelayCommand(RefreshLinks);
            EnsureParametersCommand = new RelayCommand(EnsureParameters);
            CalculateCommand = new RelayCommand(Calculate);
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
            }
            else
            {
                ParameterStatusText = "Status: Error / Missing";
                SummaryText = msg;
            }
        }

        private void Calculate()
        {
            if (SelectedLink == null)
            {
                SummaryText = "Error: Please select a Finished Ground Revit Link.";
                return;
            }

            var options = new CalculationOptions
            {
                SelectedLinkInstanceId = SelectedLink.InstanceId,
                SelectedLinkName = SelectedLink.Name,
                DisplayUnit = SelectedUnit
            };

            if (IsCurrentSelection) options.SelectionMode = SelectionMode.CurrentSelection;
            else if (IsCurrentView) options.SelectionMode = SelectionMode.CurrentView;
            else if (IsEntireModel) options.SelectionMode = SelectionMode.EntireModel;

            var selectionService = new ElementSelectionService(_logger);
            var elements = selectionService.GetSelectedElements(_doc, _uidoc, options);

            if (elements.Count == 0)
            {
                SummaryText = "Warning: No supported MEP elements found for the selected mode.";
                return;
            }

            var calcService = new DepthCalculationService(_logger);
            var results = calcService.CalculateAndApply(_doc, elements, options, out CalculationSummary summary);

            SummaryText = $"Calculation Complete!\n" +
                          $"Total Selected: {summary.TotalSelected}\n" +
                          $"Processed: {summary.Processed}\n" +
                          $"Updated: {summary.Updated}\n" +
                          $"Skipped: {summary.Skipped}\n" +
                          $"Errors: {summary.Errors}\n" +
                          $"Duration: {summary.Duration.TotalSeconds:F2}s\n" +
                          $"Log saved to Desktop.";
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
