using System;
using Autodesk.Revit.UI;

namespace MEPBuriedDepthCalculator.Services
{
    public class RevitEventService : IExternalEventHandler
    {
        private Action<UIApplication> _doWork;
        private readonly ExternalEvent _externalEvent;

        public RevitEventService()
        {
            _externalEvent = ExternalEvent.Create(this);
        }

        public void Run(Action<UIApplication> work)
        {
            _doWork = work;
            _externalEvent.Raise();
        }

        public void Execute(UIApplication app)
        {
            try
            {
                _doWork?.Invoke(app);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Revit Event Error", ex.Message);
            }
        }

        public string GetName()
        {
            return "MEP Buried Depth Event Handler";
        }
    }
}
