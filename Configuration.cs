using Dalamud.Configuration;
using Dalamud.Plugin;
using System.Collections.Generic;

namespace Nicknamer
{
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; }
        public bool Global_UseItalics = false;
        public bool Global_UseCustomColor = false;
        public ushort Global_SelectedColor = 57;
        public bool PutNicknameInFront = true;
        //public bool MatchColoredName = false;


        public Dictionary<ulong, NicknameCollection> Nicknames { get; set; } = new Dictionary<ulong, NicknameCollection>();

        private IDalamudPluginInterface pluginInterface;

        public void Initialize(IDalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface.SavePluginConfig(this);
        }
    }
}
