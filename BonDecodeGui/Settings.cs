using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;

namespace BonDecodeGui
{
    internal class Settings
    {
        public string DecodeDll { get; set; } = "B61Decoder.dll";

        public string DestinationFolder { get; set; } = ".";

        public bool AppendSuffix { get; set; } = true;

        public string Suffix { get; set; } = "_dec";

        public bool HideShell { get; set; } = true;

    }
}
