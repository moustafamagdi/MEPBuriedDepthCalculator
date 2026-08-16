using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using MEPBuriedDepthCalculator.Logging;

namespace MEPBuriedDepthCalculator.Services
{
    public class LinkedModelInfo
    {
        public ElementId InstanceId { get; set; }
        public string Name { get; set; }
        public RevitLinkInstance LinkInstance { get; set; }
        public Document LinkedDocument { get; set; }
        public Transform Transform { get; set; }
    }

    public class LinkedModelService
    {
        private readonly ILogger _logger;

        public LinkedModelService(ILogger logger)
        {
            _logger = logger;
        }

        public List<LinkedModelInfo> GetRevitLinks(Document hostDoc)
        {
            var links = new List<LinkedModelInfo>();
            try
            {
                var collector = new FilteredElementCollector(hostDoc)
                    .OfClass(typeof(RevitLinkInstance));

                foreach (RevitLinkInstance linkInst in collector)
                {
                    var linkDoc = linkInst.GetLinkDocument();
                    string name = linkInst.Name;
                    var transform = linkInst.GetTransform();

                    links.Add(new LinkedModelInfo
                    {
                        InstanceId = linkInst.Id,
                        Name = name,
                        LinkInstance = linkInst,
                        LinkedDocument = linkDoc,
                        Transform = transform
                    });
                    _logger.Info("LinkSelection", $"Found Revit Link: {name} (ID: {linkInst.Id})");
                }
            }
            catch (System.Exception ex)
            {
                _logger.Error("LinkSelection", "Error retrieving Revit link instances", ex);
            }
            return links;
        }
    }
}
