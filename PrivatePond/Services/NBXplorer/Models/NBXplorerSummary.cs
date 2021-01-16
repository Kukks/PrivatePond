using NBXplorer.Models;

namespace PrivatePond.Services.NBXplorer
{
    public class NBXplorerSummary
    {
        public NBXplorerState State { get; set; }
        public StatusResult Status { get; set; }
        public string Error { get; set; }
    }
}