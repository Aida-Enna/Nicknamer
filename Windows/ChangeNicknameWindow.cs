using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Nicknamer.Windows
{
    public class ChangeNicknameWindow : Window, IDisposable
    {
        public string PlayerName = "ERROR";
        public string PlayerWorld = "ERROR";
        //public int StartingPositionX = 0;
        //public int StartingPositionY = 0;
        public string NewNicknameString = "";
        public string OldNicknameString = "";
        public bool OverrideGlobalStyle = false;
        public bool OverrideGlobalItalics = false;
        public bool OverrideGlobalColor = false;
        public ushort OverrideGlobalColorActualColor = 57;

        // We give this window a constant ID using ###.
        // This allows for labels to be dynamic, like "{FPS Counter}fps###XYZ counter window",
        // and the window ID will always be "###XYZ counter window" for ImGui
        public ChangeNicknameWindow(Plugin plugin) : base("###NNChangeNickname")
        {
            Flags = ImGuiWindowFlags.AlwaysAutoResize;
            SizeCondition = ImGuiCond.Always;
        }

        public void Dispose()
        { }

        public override void PreDraw()
        {
            //Position = new Vector2(StartingPositionX + 50, StartingPositionY + 50);
        }

        public override void Draw()
        {
            ImGui.SetWindowFocus();
            NicknameEntry? CurrentEntry = Plugin.PluginConfig.Nicknames[Plugin.ClientState.LocalContentId].Find(x => x.PlayerName == PlayerName && x.PlayerWorld == PlayerWorld);

            if (OldNicknameString != "") { ImGui.Text("Current nickname: " + OldNicknameString); }
            ImGui.Text("New Nickname for " + PlayerName + "@" + PlayerWorld + ":");
            ImGui.SetNextItemWidth(358);
            if (ImGui.IsWindowAppearing()) { ImGui.SetKeyboardFocusHere(); }
            if (ImGui.InputText("###NewNickname", ref NewNicknameString, 420 /*haha the sex number but weed*/, ImGuiInputTextFlags.EnterReturnsTrue) && !string.IsNullOrWhiteSpace(NewNicknameString))
            {
                Plugin.PluginConfig.Nicknames[Plugin.ClientState.LocalContentId].Find(x => x.PlayerName == PlayerName && x.PlayerWorld == PlayerWorld).Nickname = NewNicknameString;
                Plugin.PluginConfig.Nicknames[Plugin.ClientState.LocalContentId].Find(x => x.PlayerName == PlayerName && x.PlayerWorld == PlayerWorld).OverrideGlobalStyle = OverrideGlobalStyle;
                Plugin.PluginConfig.Nicknames[Plugin.ClientState.LocalContentId].Find(x => x.PlayerName == PlayerName && x.PlayerWorld == PlayerWorld).OverrideGlobalItalics = OverrideGlobalItalics;
                Plugin.PluginConfig.Nicknames[Plugin.ClientState.LocalContentId].Find(x => x.PlayerName == PlayerName && x.PlayerWorld == PlayerWorld).OverrideGlobalColor = OverrideGlobalColor;
                Plugin.PluginConfig.Nicknames[Plugin.ClientState.LocalContentId].Find(x => x.PlayerName == PlayerName && x.PlayerWorld == PlayerWorld).OverrideGlobalColorActualColor = OverrideGlobalColorActualColor;
                Toggle();
                Plugin.PluginConfig.Nicknames[Plugin.ClientState.LocalContentId].Sort((a, b) => string.Compare(a.PlayerWorld, b.PlayerWorld, StringComparison.Ordinal));
                Plugin.PluginConfig.Save();
                Plugin.Chat.Print("[NN] " + PlayerName + "@" + PlayerWorld + "'s nickname has been set to: " + NewNicknameString);
            }
            ImGui.Checkbox("Override global style", ref OverrideGlobalStyle);
            if (OverrideGlobalStyle)
            {
                ImGui.Checkbox("Use italics", ref OverrideGlobalItalics);
                ImGui.Checkbox("Use custom color", ref OverrideGlobalColor);
                if (OverrideGlobalColor)
                {
                    ImGui.Text("Use this color: ");
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(50);
                    ImGui.DragUShort("####UIColor", ref OverrideGlobalColorActualColor, 1, 1, 580);
                    ImGui.SameLine();
                    ImGui.Text("(Default is 57)");
                    if (ImGui.Button("Click here to see what colors you can use"))
                    {
                        Process.Start(new ProcessStartInfo { FileName = "https://github.com/Aida-Enna/Nicknamer/tree/main?tab=readme-ov-file#choosing-a-custom-color", UseShellExecute = true });
                    }
                }
            }
            ImGui.Separator();
            if (ImGui.Button("Press enter in the text box or click here to save", new Vector2(300, 20)))
            {
                if (string.IsNullOrWhiteSpace(NewNicknameString))
                {
                    Plugin.Chat.Print("[NN] No nickname provided, save cancelled.");
                    Toggle();
                    return;
                }
                Plugin.PluginConfig.Nicknames[Plugin.ClientState.LocalContentId].Find(x => x.PlayerName == PlayerName && x.PlayerWorld == PlayerWorld).Nickname = NewNicknameString;
                Plugin.PluginConfig.Nicknames[Plugin.ClientState.LocalContentId].Find(x => x.PlayerName == PlayerName && x.PlayerWorld == PlayerWorld).OverrideGlobalStyle = OverrideGlobalStyle;
                Plugin.PluginConfig.Nicknames[Plugin.ClientState.LocalContentId].Find(x => x.PlayerName == PlayerName && x.PlayerWorld == PlayerWorld).OverrideGlobalItalics = OverrideGlobalItalics;
                Plugin.PluginConfig.Nicknames[Plugin.ClientState.LocalContentId].Find(x => x.PlayerName == PlayerName && x.PlayerWorld == PlayerWorld).OverrideGlobalColor = OverrideGlobalColor;
                Plugin.PluginConfig.Nicknames[Plugin.ClientState.LocalContentId].Find(x => x.PlayerName == PlayerName && x.PlayerWorld == PlayerWorld).OverrideGlobalColorActualColor = OverrideGlobalColorActualColor;
                Toggle();
                Plugin.PluginConfig.Nicknames[Plugin.ClientState.LocalContentId].Sort((a, b) => string.Compare(a.PlayerWorld, b.PlayerWorld, StringComparison.Ordinal));
                Plugin.PluginConfig.Save();
                Plugin.Chat.Print("[NN] " + PlayerName + "@" + PlayerWorld + "'s nickname has been set to: " + NewNicknameString);
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                Toggle();
            }
        }
    }
}