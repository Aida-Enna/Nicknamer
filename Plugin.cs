using Dalamud.Bindings.ImGui;
using Dalamud.Configuration;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Memory;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina;
using Lumina.Text.Payloads;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Veda;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode.Delegates;
using static Nicknamer.Plugin;
using TextPayload = Dalamud.Game.Text.SeStringHandling.Payloads.TextPayload;

namespace Nicknamer
{
    public class Plugin : IDalamudPlugin
    {
        public unsafe string Name => "Nicknamer";

        [PluginService] public static IDalamudPluginInterface PluginInterface { get; set; }
        [PluginService] public static IChatGui Chat { get; set; }
        [PluginService] public static IPluginLog PluginLog { get; set; }
        [PluginService] public static IGameGui GameGui { get; set; }
        [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null!;

        private PluginCommandManager<Plugin> commandManager;
        private PluginUI ui;
        public static Configuration PluginConfig { get; set; }

        public static ChangeNicknameUI ChangeNickname_UI;

        public static bool HideWindows = true;

        public unsafe Plugin(IDalamudPluginInterface pluginInterface, IChatGui chat, ICommandManager commands)
        {
            PluginInterface = pluginInterface;
            Chat = chat;

            // Get or create a configuration object
            PluginConfig = (Configuration)PluginInterface.GetPluginConfig() ?? new Configuration();
            PluginConfig.Initialize(PluginInterface);

            ChangeNickname_UI = new ChangeNicknameUI();

            ui = new PluginUI();
            PluginInterface.UiBuilder.Draw += new System.Action(ui.Draw);
            PluginInterface.UiBuilder.Draw += new System.Action(ChangeNickname_UI.Draw);
            PluginInterface.UiBuilder.OpenConfigUi += () =>
            {
                PluginUI ui = this.ui;
                ui.IsVisible = !ui.IsVisible;
            };

            // Load all of our commands
            this.commandManager = new PluginCommandManager<Plugin>(this, commands);

            Chat.ChatMessage += ChatMessage;

            ContextMenu.OnMenuOpened += OnContextMenuOpened;

            //PluginConfig.Nicknames.Entries.Add(new NicknameEntry { PlayerName = "Wicked Thunder", PlayerWorld = "Adamantoise", Nickname = "Love<3", Enabled = true });
            //PluginConfig.Nicknames.Entries.Add(new NicknameEntry { PlayerName = "Howling Blade", PlayerWorld = "Adamantoise", Nickname = "<3Love", Enabled = true });
            //PluginConfig.Save();
        }

        private void ChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            try
            {
                //if (isHandled)
                //{
                //    return;
                //}

                //TODO:
                //Have it be configurable for in front or behind player name
                //Modify color?
                //Italic or not
                //Use JSON to store what we should replace, playername+world

                foreach (PlayerPayload CurrentPlayerPayload in sender.Payloads.Where(x => x is PlayerPayload))
                {
                    int NextIndex = sender.Payloads.FindIndex(x => x is RawPayload) - 1;

                    string PlayerName = CurrentPlayerPayload.PlayerName;
                    string PlayerWorld = CurrentPlayerPayload.World.Value.Name.ExtractText();

                    NicknameEntry? CurrentNicknameEntry = PluginConfig.Nicknames.Entries.Find(x => x.PlayerName == PlayerName && x.PlayerWorld == PlayerWorld);

                    if (CurrentNicknameEntry == null) { continue; }
                    if (CurrentNicknameEntry.Enabled == false) { continue; }

                    sender.Payloads.Insert(NextIndex, new TextPayload(" (")); NextIndex++;
                    if (PluginConfig.UseItalics) { sender.Payloads.Insert(NextIndex, new EmphasisItalicPayload(true)); NextIndex++; }
                    sender.Payloads.Insert(NextIndex, new TextPayload(CurrentNicknameEntry.Nickname)); NextIndex++;
                    if (PluginConfig.UseItalics) { sender.Payloads.Insert(NextIndex, new EmphasisItalicPayload(false)); NextIndex++; }
                    sender.Payloads.Insert(NextIndex, new TextPayload(")")); NextIndex++;
                }
            }
            catch (Exception f)
            {
                // Ignore exception
            }
        }

        private void OnContextMenuOpened(IMenuOpenedArgs args)
        {
            try
            {
                switch (args.AddonName)
                {
                    case null:
                    case "LookingForGroup":
                    case "PartyMemberList":
                    case "FriendList":
                    case "FreeCompany":
                    case "SocialList":
                    case "ContactList":
                    case "ChatLog":
                    case "_PartyList":
                    case "LinkShell":
                    case "CrossWorldLinkshell":
                    case "ContentMemberList":
                    case "BeginnerChatList":
                        if (((MenuTargetDefault)args.Target).TargetName != string.Empty && ((MenuTargetDefault)args.Target).TargetHomeWorld.Value.RowId != 0)
                        {
                            var OpenNicknameSubMenu = new MenuItem();
                            OpenNicknameSubMenu.Name = "Nickname";
                            OpenNicknameSubMenu.Prefix = SeIconChar.BoxedLetterN;
                            OpenNicknameSubMenu.PrefixColor = 1;
                            OpenNicknameSubMenu.IsSubmenu = true;
                            OpenNicknameSubMenu.OnClicked += args => PopulateNicknameOptions(args);
                            OpenNicknameSubMenu.Priority = 0;
                            args.AddMenuItem(OpenNicknameSubMenu);
                        }
                        break;
                }
            }
            catch (Exception f)
            {
                // Ignore exception
            }
        }

        private void PopulateNicknameOptions(IMenuItemClickedArgs clickedArgs)
        {
            try
            {
                var menuItems = new List<MenuItem>();

                string PlayerName = ((MenuTargetDefault)clickedArgs.Target).TargetName;
                string PlayerWorld = ((MenuTargetDefault)clickedArgs.Target).TargetHomeWorld.Value.Name.ExtractText();
                NicknameEntry? CurrentNicknameEntry = PluginConfig.Nicknames.Entries.Find(x => x.PlayerName == PlayerName && x.PlayerWorld == PlayerWorld);

                var RemoveNicknameMenuItem = new MenuItem
                {
                    Name = "Remove Nickname",
                    Prefix = SeIconChar.ArrowRight,
                    OnClicked = clickedArgs => RemoveNickname(CurrentNicknameEntry)
                };
                var AddNicknameMenuItem = new MenuItem
                {
                    Name = "Add Nickname",
                    Prefix = SeIconChar.ArrowRight,
                    OnClicked = clickedArgs => AddNickname(PlayerName, PlayerWorld)
                };
                var ChangeNicknameMenuItem = new MenuItem
                {
                    Name = "Change Nickname",
                    Prefix = SeIconChar.ArrowRight,
                    OnClicked = clickedArgs => ChangeNickname(CurrentNicknameEntry)
                };

                if (CurrentNicknameEntry != null)
                {
                    menuItems.Add(ChangeNicknameMenuItem);
                    menuItems.Add(RemoveNicknameMenuItem);
                    clickedArgs.OpenSubmenu("Current Nickname: " + CurrentNicknameEntry.Nickname, menuItems);
                }
                else
                {
                    menuItems.Add(AddNicknameMenuItem);
                    clickedArgs.OpenSubmenu("No Nickname Set", menuItems);
                }
            }
            catch (Exception ex)
            {
                Chat.Print("An error has occured - " + ex.ToString());
            }
        }

        public static unsafe void AddNickname(string PlayerName, string PlayerWorld)
        {
            var ContextMenuPtr = GameGui.GetAddonByName("ContextMenu", 1);
            var addonContextMenu = (AddonContextMenu*)ContextMenuPtr.Address;
            ChangeNickname_UI.NewNicknameString = "";
            ChangeNickname_UI.PlayerName = PlayerName;
            ChangeNickname_UI.PlayerWorld = PlayerWorld;
            ChangeNickname_UI.StartingPositionX = addonContextMenu->X;
            ChangeNickname_UI.StartingPositionY = addonContextMenu->Y;
            ChangeNickname_UI.OldNicknameString = "";
            ChangeNickname_UI.IsVisible = true;
        }

        public static unsafe void ChangeNickname(NicknameEntry? currentNicknameEntry)
        {
            var ContextMenuPtr = GameGui.GetAddonByName("ContextMenu", 1);
            var addonContextMenu = (AddonContextMenu*)ContextMenuPtr.Address;
            ChangeNickname_UI.NewNicknameString = "";
            ChangeNickname_UI.PlayerName = currentNicknameEntry.PlayerName;
            ChangeNickname_UI.PlayerWorld = currentNicknameEntry.PlayerWorld;
            ChangeNickname_UI.StartingPositionX = addonContextMenu->X;
            ChangeNickname_UI.StartingPositionY = addonContextMenu->Y;
            ChangeNickname_UI.OldNicknameString = currentNicknameEntry.Nickname;
            ChangeNickname_UI.IsVisible = true;
        }

        public static void RemoveNickname(NicknameEntry? currentNicknameEntry)
        {
            PluginConfig.Nicknames.Entries.Remove(currentNicknameEntry);
            PluginConfig.Save();
        }

        [Command("/nickname")]
        [Aliases("/nicknamer", "/nn")]
        [HelpMessage("Shows the config menu")]
        public void ToggleConfig(string command, string args)
        {
            ui.IsVisible = !ui.IsVisible;
        }

        public class ChangeNicknameUI
        {
            public string PlayerName = "ERROR";
            public string PlayerWorld = "ERROR";
            public int StartingPositionX = 0;
            public int StartingPositionY = 0;
            public bool IsVisible = false;
            public string NewNicknameString = "";
            public string OldNicknameString = "";

            public void Draw()
            {
                if (!IsVisible || !ImGui.Begin("", ref IsVisible, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize)) { return; }
                ImGui.SetWindowPos(new Vector2(StartingPositionX + 50, StartingPositionY + 50));
                ImGui.SetWindowFocus();
                ImGui.Text("New Nickname for " + PlayerName + "@" + PlayerWorld + ":");
                if (OldNicknameString != "") { ImGui.Text("Current nickname: " + OldNicknameString); }
                ImGui.SetNextItemWidth(300);
                if (ImGui.IsWindowAppearing()) { ImGui.SetKeyboardFocusHere(); }
                if (ImGui.InputText("###NewNickname", ref NewNicknameString, 420 /*haha the sex number but weed*/, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    NicknameEntry? CurrentEntry = PluginConfig.Nicknames.Entries.Find(x => x.PlayerName == PlayerName && x.PlayerWorld == PlayerWorld);
                    if (CurrentEntry == null)
                    {
                        PluginConfig.Nicknames.Entries.Add(new NicknameEntry { PlayerName = PlayerName, PlayerWorld = PlayerWorld, Nickname = NewNicknameString, Enabled = true });
                    }
                    else
                    {
                        PluginConfig.Nicknames.Entries.Find(x => x.PlayerName == PlayerName && x.PlayerWorld == PlayerWorld).Nickname = NewNicknameString;
                    }
                    this.IsVisible = false;
                    PluginConfig.Save();
                }
                if (ImGui.Button("Press enter to save or click here to cancel", new Vector2(300, 20)))
                {
                    this.IsVisible = false;
                }
                ImGui.End();
            }
        }

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            this.commandManager.Dispose();

            PluginInterface.SavePluginConfig(PluginConfig);

            PluginInterface.UiBuilder.Draw -= ui.Draw;
            PluginInterface.UiBuilder.Draw -= new System.Action(ChangeNickname_UI.Draw);
            PluginInterface.UiBuilder.OpenConfigUi -= () =>
            {
                PluginUI ui = this.ui;
                ui.IsVisible = !ui.IsVisible;
            };

            Chat.ChatMessage -= ChatMessage;
            ContextMenu.OnMenuOpened -= OnContextMenuOpened;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion IDisposable Support
    }
}