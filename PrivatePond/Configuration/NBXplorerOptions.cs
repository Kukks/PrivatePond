using System;

namespace PrivatePond.Data
{
    public class NBXplorerOptions
    {
        public const string OptionsConfigSection = "NBXplorer";
        public Uri ExplorerUri { get;  set; }
        public string CookieFile { get; set; }
    }
}