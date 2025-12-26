using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using System;
using System.Diagnostics;
using System.Linq;
using Veda;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Nicknamer.Windows
{
    public class ConfigWindow : Window, IDisposable
    {

        // We give this window a constant ID using ###.
        // This allows for labels to be dynamic, like "{FPS Counter}fps###XYZ counter window",
        // and the window ID will always be "###XYZ counter window" for ImGui
        public ConfigWindow(Plugin plugin) : base("Nicknamer Config###NNConfig")
        {
            Flags = ImGuiWindowFlags.AlwaysAutoResize;


            SizeCondition = ImGuiCond.Always;

        }
        public void Dispose() { }

        private bool ShowSupport;
        public override void Draw()
        {
            ImGui.Text("Global customization options for all nicknames:");
            ImGui.Checkbox("Put nickname in front of player name instead of behind", ref Plugin.PluginConfig.PutNicknameInFront);
            ImGui.Checkbox("Use italics", ref Plugin.PluginConfig.Global_UseItalics);
            ImGui.Checkbox("Use custom color", ref Plugin.PluginConfig.Global_UseCustomColor);
            //if (ImGui.Checkbox("Use custom color", ref Plugin.PluginConfig.Global_UseCustomColor))
            //{
            //    Plugin.PluginConfig.MatchColoredName = false;
            //}
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
                    Dalamud.Utility.Util.OpenLink("https://github.com/Aida-Enna/Nicknamer/tree/main?tab=readme-ov-file#choosing-a-custom-color");
                }
            }
            //if (ImGui.Checkbox("Use colors from simple tweaks", ref Plugin.PluginConfig.MatchColoredName))
            //{
            //    Plugin.PluginConfig.Global_UseCustomColor = false;
            //}
            //if (Plugin.PluginConfig.MatchColoredName)
            //{
            //    if (!Plugin.PluginInterface.InstalledPlugins.Any(x => x.InternalName == "SimpleTweaksPlugin"))
            //    {
            //        //var Plugins = Plugin.PluginInterface.InstalledPlugins.Where(x => x.InternalName.Contains("Tweak")).First();
            //        ImGui.TextColored(new System.Numerics.Vector4(1f, 0f, 0f, 1f), "You do not have Simple Tweaks installed, so this option will do nothing.\nIf you have another plugin installed that is modifying colors, you can\nignore this error but things may not work as expected.");
            //    }
            //}
            ImGui.Text("Per-player overrides are available from the\nright click -> nickname menu or the main window.");
            if (ImGui.Button("Want to help support my work?"))
            {
                ShowSupport = !ShowSupport;
            }
            if (ImGui.IsItemHovered()) { ImGui.SetTooltip("Click me!"); }
            if (ShowSupport)
            {
                ImGui.Text("Here are the current ways you can support the work I do.\nEvery bit helps, thank you! Have a great day!");
                ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.19f, 0.52f, 0.27f, 1));
                if (ImGui.Button("Donate via Paypal"))
                {
                    Dalamud.Utility.Util.OpenLink("https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=QXF8EL4737HWJ");
                }
                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.95f, 0.39f, 0.32f, 1));
                if (ImGui.Button("Become a Patron"))
                {
                    Dalamud.Utility.Util.OpenLink("https://www.patreon.com/bePatron?u=5597973");
                }
                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.25f, 0.67f, 0.87f, 1));
                if (ImGui.Button("Support me on Ko-Fi"))
                {
                    Dalamud.Utility.Util.OpenLink("https://ko-fi.com/Y8Y114PMT");
                }
                ImGui.PopStyleColor();
            }
        }
    }
}
