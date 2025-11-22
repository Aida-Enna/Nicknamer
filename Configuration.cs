using Dalamud.Configuration;
using Dalamud.Plugin;

namespace Nicknamer
{
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; }
        public bool UseItalics = false;
        public bool UseCustomColor = false;
        public int SelectedColor = 0;

        public NicknameCollection Nicknames { get; set; } = new NicknameCollection { };

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
