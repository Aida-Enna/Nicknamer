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

            ImGui.Checkbox("Use italics", ref Plugin.PluginConfig.UseItalics);
            ImGui.Checkbox("Use Custom Color", ref Plugin.PluginConfig.UseCustomColor);
            if (Plugin.PluginConfig.UseCustomColor)
            {
                ImGui.Text("Use this color: ");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(50);
                ImGui.DragInt("####UIColor", ref Plugin.PluginConfig.SelectedColor, 1, 0, 580);
                if (ImGui.Button("Save"))
                {
                    Plugin.PluginConfig.Save();
                    if (ImGui.Button("Click here to see what colors you can use"))
                    {
                        Process.Start(new ProcessStartInfo { FileName = URL, UseShellExecute = true });
                    }
                }
            }
            ImGui.End();
        }
    }
}
