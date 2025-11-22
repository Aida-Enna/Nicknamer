using Dalamud.Bindings.ImGui;
using System;
using System.Diagnostics;
using System.Linq;
using Veda;

namespace Nicknamer
{
    public class PluginUI
    {
        public bool IsVisible;
        private bool ShowSupport;
        public void Draw()
        {
            if (!IsVisible || !ImGui.Begin("Nicknamer Config", ref IsVisible, ImGuiWindowFlags.AlwaysAutoResize))
                return;
            ImGui.Text("Global customization options for all nicknames:");
            ImGui.Checkbox("Use italics", ref Plugin.PluginConfig.Global_UseItalics);
            ImGui.Checkbox("Use Custom Color", ref Plugin.PluginConfig.Global_UseCustomColor);
            if (Plugin.PluginConfig.Global_UseCustomColor)
            {
                ImGui.Text("Use this color: ");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(50);
                ImGui.DragUShort("####UIColor", ref Plugin.PluginConfig.Global_SelectedColor, 1, 1, 580);
                ImGui.SameLine();
                ImGui.Text("(Default is 57)");
                if (ImGui.Button("Click here to see what colors you can use"))
                {
                    Process.Start(new ProcessStartInfo { FileName = "https://github.com/Aida-Enna/Nicknamer/tree/main?tab=readme-ov-file#choosing-a-custom-color", UseShellExecute = true });
                }
            }
            ImGui.Text("Per-player overrides are available from the right click -> nickname menu.");
            ImGui.End();
        }
    }
}
