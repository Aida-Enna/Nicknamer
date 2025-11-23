using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Veda;

namespace Nicknamer.Windows
{
    public class MainWindow : Window, IDisposable
    {

        // We give this window a constant ID using ###.
        // This allows for labels to be dynamic, like "{FPS Counter}fps###XYZ counter window",
        // and the window ID will always be "###XYZ counter window" for ImGui
        public MainWindow(Plugin plugin) : base("Nicknamer###NNMainWindow")
        {
            Flags = ImGuiWindowFlags.AlwaysAutoResize;
            SizeCondition = ImGuiCond.Always;

        }
        public void Dispose() { }

        public override void PreDraw()
        {

        }

        private bool ShowSupport;
        public override void Draw()
        {
            ImGui.Text(Plugin.ClientState.LocalPlayer.Name + "@" + Plugin.ClientState.LocalPlayer.HomeWorld.Value.Name.ExtractText() + " has set the following nicknames and overrides:");
            if (ImGui.BeginTable($"##TotalStatsTable", 7, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit))
            {
                ImGui.TableNextColumn();
                ImGui.TextUnformatted("Player Name");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted("Player World");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted("Nickname");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted("Italics");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted("Color");
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();

                ImGui.TableNextRow();

                foreach (var (index, name) in Plugin.PluginConfig.Nicknames[Plugin.ClientState.LocalContentId].Index())
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
                        Plugin.ChangeNickname(Plugin.PluginConfig.Nicknames[Plugin.ClientState.LocalContentId][index]);
                    }
                    ImGui.TableNextColumn();
                    if (ImGui.Button("Delete"))
                    {
                        Plugin.RemoveNickname(Plugin.PluginConfig.Nicknames[Plugin.ClientState.LocalContentId][index]);
                    }
                }
                ImGui.EndTable();
            }
        }
    }
}
