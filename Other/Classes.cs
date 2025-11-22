using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nicknamer
{
    public class NicknameCollection
    {
        public List<NicknameEntry> Entries { get; set; } = new();
    }

    public class NicknameEntry
    {
        public string PlayerName { get; set; }
        public string PlayerWorld { get; set; }
        public string Nickname { get; set; }
        public bool Enabled { get; set; }
    }
}
