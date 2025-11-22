using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nicknamer
{
    public class NicknameCollection : List<NicknameEntry>
    {
        public ulong OwnerID { get; set; }
    }

    public class NicknameEntry
    {
        public string PlayerName { get; set; }
        public string PlayerWorld { get; set; }
        public string Nickname { get; set; }
        public bool Enabled { get; set; }
        public ulong ContentID { get; set; }
        public bool OverrideGlobalStyle { get; set; }
        public bool OverrideGlobalItalics { get; set; }
        public bool OverrideGlobalColor { get; set; }
        public ushort OverrideGlobalColorActualColor { get; set; }
    }
}
