using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using static FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.VertexShader;

namespace Nicknamer.Windows
{
    public class MainWindow : Window, IDisposable
    {
        private static ExcelSheet<World>? worldSheet = Plugin.DataManager.GetExcelSheet<World>();

        // We give this window a constant ID using ###.
        // This allows for labels to be dynamic, like "{FPS Counter}fps###XYZ counter window",
        // and the window ID will always be "###XYZ counter window" for ImGui
        public MainWindow(Plugin plugin) : base("Nicknamer###NNMainWindow")
        {
            Flags = ImGuiWindowFlags.AlwaysAutoResize;
            SizeCondition = ImGuiCond.Always;
        }

        public void Dispose()
        { }

        public override void PreDraw()
        {
        }

        private bool ShowSupport;
        public string PlayerToAddName = "";
        public string PlayerToAddWorld = "";

        public override void Draw()
        {
            List<NicknameEntry> ToRemove = new();
            if (string.IsNullOrWhiteSpace(Plugin.PlayerState.CharacterName) || Plugin.PlayerState is null || !Plugin.ClientState.IsLoggedIn) { return; }
            ImGui.Text(Plugin.PlayerState.CharacterName + "@" + Plugin.PlayerState.HomeWorld.Value.Name.ExtractText() + " has set the following nicknames and overrides:");
            if (ImGui.BeginTable($"##TotalStatsTable", 7, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit))
            {
                ImGui.TableSetupColumn("Player Name");
                ImGui.TableSetupColumn("Player World");
                ImGui.TableSetupColumn("Nickname");
                ImGui.TableSetupColumn("Italics");
                ImGui.TableSetupColumn("Color");
                ImGui.TableHeadersRow();

                foreach (var (index, name) in Plugin.PluginConfig.Nicknames[Plugin.PlayerState.ContentId].Index())
                {
                    if (string.IsNullOrWhiteSpace(name.Nickname)) { continue; }
                    using var id = ImRaii.PushId(index);
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(name.PlayerName);
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(name.PlayerWorld);
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(name.Nickname);
                    ImGui.TableNextColumn();
                    if (name.OverrideGlobalStyle)
                    {
                        if (name.OverrideGlobalItalics)
                        {
                            ImGui.TextUnformatted("Yes");
                            ImGui.TableNextColumn();
                        }
                        else
                        {
                            ImGui.TableNextColumn();
                        }
                        if (name.OverrideGlobalColor)
                        {
                            ImGui.TextUnformatted(name.OverrideGlobalColorActualColor.ToString());
                            ImGui.TableNextColumn();
                        }
                        else
                        {
                            ImGui.TableNextColumn();
                        }
                    }
                    else
                    {
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                    }
                    if (ImGui.Button("Change"))
                    {
                        Plugin.ChangeNickname(Plugin.PluginConfig.Nicknames[Plugin.PlayerState.ContentId][index]);
                    }
                    ImGui.TableNextColumn();
                    if (ImGui.Button("Delete"))
                    {
                        ToRemove.Add(Plugin.PluginConfig.Nicknames[Plugin.PlayerState.ContentId][index]);
                    }
                }
                ImGui.EndTable();
                ImGui.Text("Name: ");
                ImGui.SameLine();
                ImGui.Indent(300);
                ImGui.Text("World: ");
                ImGui.Unindent(300);
                ImGui.InputText("##Name", ref PlayerToAddName);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(180);
                if (ImGui.BeginCombo("###World", string.IsNullOrWhiteSpace(PlayerToAddWorld) ? "Not Selected" : PlayerToAddWorld))
                {
                    foreach (World w in worldSheet.Where(w => w.IsPublic).OrderBy(x => x.Name.ToString()))
                    {
                        if (ImGui.Selectable(w.Name.ToString()))
                        {
                            PlayerToAddWorld = w.Name.ToString();
                        }
                    }
                    ImGui.EndCombo();
                }
                ImGui.SameLine();
                if (ImGui.Button("Add"))
                {
                    if (string.IsNullOrWhiteSpace(PlayerToAddName) || PlayerToAddName.Count(c => c == ' ') != 1)
                    {
                        Plugin.Chat.Print("Invalid name, please check it again.");
                        return;
                    }
                    if (string.IsNullOrWhiteSpace(PlayerToAddWorld) || PlayerToAddWorld == "Not Selected")
                    {
                        Plugin.Chat.Print("Invalid world, please check it again.");
                        return;
                    }

                    Plugin.AddNickname(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(PlayerToAddName.ToLower()), PlayerToAddWorld);
                }
            }
            foreach (var Item in ToRemove)
            {
                Plugin.RemoveNickname(Item);
            }
        }
    }
}