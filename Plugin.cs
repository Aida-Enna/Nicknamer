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
using Dalamud.Interface.Windowing;
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
using Lumina.Excel.Sheets;
using Lumina.Excel.Sheets.Experimental;
using Lumina.Text.Payloads;
using Nicknamer.Windows;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Veda;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode.Delegates;
using static Lumina.Data.Parsing.Layer.LayerCommon;
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
        [PluginService] public static IClientState ClientState { get; set; } = null!;
        [PluginService] public static IContextMenu ContextMenu { get; private set; } = null!;
        [PluginService] public static IDataManager DataManager { get; private set; } = null!;
        [PluginService] public static IPlayerState PlayerState { get; private set; } = null!;

        private PluginCommandManager<Plugin> commandManager;
        public static Configuration PluginConfig { get; set; }

        public readonly WindowSystem WindowSystem = new("Nicknamer");
        private ConfigWindow ConfigWindow { get; init; }
        public static ChangeNicknameWindow ChangeNicknameWindow { get; set; }
        private MainWindow MainWindow { get; init; }

        public Plugin(IDalamudPluginInterface pluginInterface, IChatGui chat, ICommandManager commands, IClientState clientState, IPlayerState playerState)
        {
            // Get or create a configuration object
            PluginConfig = (Configuration)PluginInterface.GetPluginConfig() ?? new Configuration();
            PluginConfig.Initialize(PluginInterface);

            ConfigWindow = new ConfigWindow(this);
            ChangeNicknameWindow = new ChangeNicknameWindow(this);
            MainWindow = new MainWindow(this);

            WindowSystem.AddWindow(ConfigWindow);
            WindowSystem.AddWindow(ChangeNicknameWindow);
            WindowSystem.AddWindow(MainWindow);

            PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
            PluginInterface.UiBuilder.OpenMainUi += MainWindow.Toggle;
            PluginInterface.UiBuilder.OpenConfigUi += ConfigWindow.Toggle;

            // Load all of our commands
            this.commandManager = new PluginCommandManager<Plugin>(this, commands);

            Chat.ChatMessage += ChatMessage;
            ContextMenu.OnMenuOpened += OnContextMenuOpened;
        }

        private void ChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            try
            {
                if (!ClientState.IsLoggedIn) { return; }

                if (sender.Payloads.Count == 0) { return; }

                if (!PluginConfig.Nicknames.ContainsKey(PlayerState.ContentId))
                {
                    PluginConfig.Nicknames.Add(PlayerState.ContentId, new NicknameCollection());
                    PluginConfig.Save();
                }

                var builder = new SeStringBuilder();
                var NewPayloads = new List<Payload>();

                string PlayerName = "Player";
                string PlayerWorld = "World";
                bool ClearedPlayerPayloadAlready = false;
                List<Payload> NicknamePayload = new List<Payload>();
                foreach (Payload payload in sender.Payloads)
                {
                    if (payload is PlayerPayload)
                    {
                        PlayerName = (payload as PlayerPayload).PlayerName;
                        PlayerWorld = (payload as PlayerPayload).World.Value.Name.ExtractText();

                        NicknameEntry? CurrentNicknameEntry = PluginConfig.Nicknames[PlayerState.ContentId].Find(x => x.PlayerName == PlayerName && x.PlayerWorld == PlayerWorld);

                        if (CurrentNicknameEntry == null) { return; }
                        if (CurrentNicknameEntry.Enabled == false) { return; }
                        if (String.IsNullOrWhiteSpace(CurrentNicknameEntry.Nickname)) { return; }

                        //If we've set a global custom color but NOT an override
                        if (PluginConfig.Global_UseCustomColor && CurrentNicknameEntry.OverrideGlobalStyle == false)
                        {
                            //Apply the color
                            NicknamePayload.Add(new UIForegroundPayload(Plugin.PluginConfig.Global_SelectedColor));
                        }
                        //If we've set an override
                        if (CurrentNicknameEntry.OverrideGlobalStyle && CurrentNicknameEntry.OverrideGlobalColor)
                        {
                            //Apply the color
                            NicknamePayload.Add(new UIForegroundPayload(CurrentNicknameEntry.OverrideGlobalColorActualColor));
                        }
                        //If we have global or override italics on
                        if (PluginConfig.Global_UseItalics || (CurrentNicknameEntry.OverrideGlobalStyle && CurrentNicknameEntry.OverrideGlobalItalics))
                        {
                            //Apply italics
                            NicknamePayload.Add(new EmphasisItalicPayload(true));
                        }
                        //Put the name in
                        if (PluginConfig.PutNicknameInFront)
                        {
                            NicknamePayload.Add(new TextPayload("(" + CurrentNicknameEntry.Nickname + ") "));
                        }
                        else
                        {
                            NicknamePayload.Add(new TextPayload(" (" + CurrentNicknameEntry.Nickname + ")"));
                        }
                        //If we have global or override italics on, end them here
                        if (PluginConfig.Global_UseItalics || (CurrentNicknameEntry.OverrideGlobalStyle && CurrentNicknameEntry.OverrideGlobalItalics))
                        {
                            NicknamePayload.Add(new EmphasisItalicPayload(false));
                        }
                        //end the color
                        if (PluginConfig.Global_UseCustomColor || (CurrentNicknameEntry.OverrideGlobalStyle && CurrentNicknameEntry.OverrideGlobalColor))
                        {
                            NicknamePayload.Add(new UIForegroundPayload(0));
                        }
                    }
                    if (PluginConfig.PutNicknameInFront)
                    {
                        if (payload is TextPayload)
                        {
                            //Add our thing THEN the name payload
                            if (Plugin.PluginConfig.MatchColoredName)
                            {
                                NewPayloads.AddRange(NicknamePayload);
                            }
                            else
                            {
                                UIForegroundPayload FirstUIPayload = (UIForegroundPayload)sender.Payloads.Where(x => x is UIForegroundPayload).First();
                                NewPayloads.Remove(FirstUIPayload);
                                NewPayloads.AddRange(NicknamePayload);
                                NewPayloads.Add(FirstUIPayload);
                            }
                            //NewPayloads.Add(payload);
                        }
                    }

                    if (payload is RawPayload)
                    {
                        if (!PluginConfig.PutNicknameInFront)
                        {
                            //Add the player payload THEN our thing
                            //NewPayloads.Add(payload);)
                            NewPayloads.AddRange(NicknamePayload);
                        }
                        Thread.Sleep(1);
                    }
                    if (payload is UIForegroundPayload && PluginConfig.MatchColoredName && !ClearedPlayerPayloadAlready)
                    {
                        if ((payload as UIForegroundPayload).ColorKey == 0)
                        {
                            ClearedPlayerPayloadAlready = true;
                            continue;
                        }
                    }
                    NewPayloads.Add(payload);
                }

                if (NewPayloads.Count > 1)
                {
                    sender.Payloads.Clear();
                    sender.Payloads.AddRange(NewPayloads);
                    if (PluginConfig.MatchColoredName) { sender.Payloads.Insert(sender.Payloads.Count() - 1, new UIForegroundPayload(0)); }
                    Thread.Sleep(1);
                }

                //Old Method

                //foreach (PlayerPayload CurrentPlayerPayload in sender.Payloads.Where(x => x is PlayerPayload))
                //{
                //    int NextIndex = sender.Payloads.FindIndex(x => x is RawPayload);

                //    if (Plugin.PluginConfig.MatchColoredName) { NextIndex--; }

                //    // Possible that GMs return a null payload (Thanks infi!)
                //    if (CurrentPlayerPayload == null) { return; }

                //    if (PluginConfig.PutNicknameInFront)
                //    {
                //        if (Plugin.PluginConfig.MatchColoredName)
                //        {
                //            NextIndex = sender.Payloads.FindIndex(x => x is RawPayload) - 2;
                //        }
                //        else
                //        {
                //            NextIndex = 1;
                //        }
                //    }

                //    string PlayerName = CurrentPlayerPayload.PlayerName;
                //    string PlayerWorld = CurrentPlayerPayload.World.Value.Name.ExtractText();

                //    NicknameEntry? CurrentNicknameEntry = PluginConfig.Nicknames[PlayerState.ContentId].Find(x => x.PlayerName == PlayerName && x.PlayerWorld == PlayerWorld);

                //    if (CurrentNicknameEntry == null) { continue; }
                //    if (CurrentNicknameEntry.Enabled == false) { continue; }
                //    if (String.IsNullOrWhiteSpace(CurrentNicknameEntry.Nickname)) { return; }

                //    //If we've set a global custom color but NOT an override
                //    if (PluginConfig.Global_UseCustomColor && CurrentNicknameEntry.OverrideGlobalStyle == false)
                //    {
                //        //Apply the color
                //        sender.Payloads.Insert(NextIndex, new UIForegroundPayload(Plugin.PluginConfig.Global_SelectedColor)); NextIndex++;
                //    }
                //    //If we've set an override
                //    if (CurrentNicknameEntry.OverrideGlobalStyle && CurrentNicknameEntry.OverrideGlobalColor)
                //    {
                //        //Apply the color
                //        sender.Payloads.Insert(NextIndex, new UIForegroundPayload(CurrentNicknameEntry.OverrideGlobalColorActualColor)); NextIndex++;
                //    }
                //    //Insert the start of the new text payload
                //    if (PluginConfig.PutNicknameInFront)
                //    {
                //        sender.Payloads.Insert(NextIndex, new TextPayload("(")); NextIndex++;
                //    }
                //    else
                //    {
                //        sender.Payloads.Insert(NextIndex, new TextPayload(" (")); NextIndex++;
                //    }
                //    //If we have global or override italics on
                //    if (PluginConfig.Global_UseItalics || (CurrentNicknameEntry.OverrideGlobalStyle && CurrentNicknameEntry.OverrideGlobalItalics))
                //    {
                //        //Apply italics
                //        sender.Payloads.Insert(NextIndex, new EmphasisItalicPayload(true)); NextIndex++;
                //    }
                //    //Put the name in
                //    sender.Payloads.Insert(NextIndex, new TextPayload(CurrentNicknameEntry.Nickname)); NextIndex++;
                //    //If we have global or override italics on, end them here
                //    if (PluginConfig.Global_UseItalics || (CurrentNicknameEntry.OverrideGlobalStyle && CurrentNicknameEntry.OverrideGlobalItalics))
                //    {
                //        sender.Payloads.Insert(NextIndex, new EmphasisItalicPayload(false)); NextIndex++;
                //    }
                //    //end of the text
                //    if (PluginConfig.PutNicknameInFront)
                //    {
                //        sender.Payloads.Insert(NextIndex, new TextPayload(") ")); NextIndex++;
                //    }
                //    else
                //    {
                //        sender.Payloads.Insert(NextIndex, new TextPayload(")")); NextIndex++;
                //    }
                //    //end the color
                //    if (PluginConfig.Global_UseCustomColor || (CurrentNicknameEntry.OverrideGlobalStyle && CurrentNicknameEntry.OverrideGlobalColor))
                //    {
                //        sender.Payloads.Insert(NextIndex, new UIForegroundPayload(0)); NextIndex++;
                //    }
                //}
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
                //Shamelessly copied from PlayerTrack - Thank you!
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
                NicknameEntry? CurrentNicknameEntry = PluginConfig.Nicknames[PlayerState.ContentId].Find(x => x.PlayerName == PlayerName && x.PlayerWorld == PlayerWorld);

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
            ChangeNicknameWindow.NewNicknameString = "";
            ChangeNicknameWindow.PlayerName = PlayerName;
            ChangeNicknameWindow.PlayerWorld = PlayerWorld;
            ChangeNicknameWindow.OldNicknameString = "";
            NicknameEntry? currentNicknameEntry = PluginConfig.Nicknames[PlayerState.ContentId].Find(x => x.PlayerName == PlayerName && x.PlayerWorld == PlayerWorld);

            if (currentNicknameEntry == null)
            {
                PluginConfig.Nicknames[PlayerState.ContentId].Add(new NicknameEntry { PlayerName = PlayerName, PlayerWorld = PlayerWorld, Nickname = "", Enabled = true, ContentID = 0, OverrideGlobalItalics = false, OverrideGlobalStyle = false, OverrideGlobalColor = false, OverrideGlobalColorActualColor = 57 });
                PluginConfig.Save();
                currentNicknameEntry = PluginConfig.Nicknames[PlayerState.ContentId].Find(x => x.PlayerName == PlayerName && x.PlayerWorld == PlayerWorld);
            }
            ChangeNicknameWindow.OverrideGlobalStyle = currentNicknameEntry.OverrideGlobalStyle;
            ChangeNicknameWindow.OverrideGlobalItalics = currentNicknameEntry.OverrideGlobalItalics;
            ChangeNicknameWindow.OverrideGlobalColor = currentNicknameEntry.OverrideGlobalColor;
            ChangeNicknameWindow.OverrideGlobalColorActualColor = currentNicknameEntry.OverrideGlobalColorActualColor;

            ChangeNicknameWindow.Toggle();
        }

        public static unsafe void ChangeNickname(NicknameEntry? currentNicknameEntry)
        {
            var ContextMenuPtr = GameGui.GetAddonByName("ContextMenu", 1);
            var addonContextMenu = (AddonContextMenu*)ContextMenuPtr.Address;
            ChangeNicknameWindow.PlayerName = currentNicknameEntry.PlayerName;
            ChangeNicknameWindow.PlayerWorld = currentNicknameEntry.PlayerWorld;
            ChangeNicknameWindow.OldNicknameString = currentNicknameEntry.Nickname;
            ChangeNicknameWindow.NewNicknameString = currentNicknameEntry.Nickname;
            ChangeNicknameWindow.OverrideGlobalStyle = currentNicknameEntry.OverrideGlobalStyle;
            ChangeNicknameWindow.OverrideGlobalItalics = currentNicknameEntry.OverrideGlobalItalics;
            ChangeNicknameWindow.OverrideGlobalColor = currentNicknameEntry.OverrideGlobalColor;
            ChangeNicknameWindow.OverrideGlobalColorActualColor = currentNicknameEntry.OverrideGlobalColorActualColor;

            ChangeNicknameWindow.Toggle();
        }

        public static void RemoveNickname(NicknameEntry? currentNicknameEntry)
        {
            PluginConfig.Nicknames[PlayerState.ContentId].Remove(currentNicknameEntry);
            PluginConfig.Nicknames[Plugin.PlayerState.ContentId].Sort((a, b) => string.Compare(a.PlayerWorld, b.PlayerWorld, StringComparison.Ordinal));
            PluginConfig.Save();
            Chat.Print("[NN] " + currentNicknameEntry.PlayerName + "@" + currentNicknameEntry.PlayerWorld + "'s nickname has been removed.");
            FixNicknameEntries();
        }

        public static void FixNicknameEntries()
        {
            List<NicknameEntry> EntriesToRemove = new();
            foreach (NicknameEntry Entry in PluginConfig.Nicknames[PlayerState.ContentId])
            {
                if (string.IsNullOrWhiteSpace(Entry.Nickname))
                {
                    EntriesToRemove.Add(Entry);
                }
            }
            foreach (NicknameEntry EntryToRemove in EntriesToRemove)
            {
                PluginConfig.Nicknames[PlayerState.ContentId].Remove(EntryToRemove);
                PluginConfig.Nicknames[Plugin.PlayerState.ContentId].Sort((a, b) => string.Compare(a.PlayerWorld, b.PlayerWorld, StringComparison.Ordinal));
                PluginConfig.Save();
            }
        }

        [Command("/nickname")]
        [Aliases("/nicknamer", "/nn")]
        [HelpMessage("Shows the main window")]
        public void ToggleMain(string command, string args)
        {
            MainWindow.Toggle();
        }

        [Command("/nicknameconfig")]
        [Aliases("/nnconfig", "/nnc")]
        [HelpMessage("Shows the config menu")]
        public void ToggleConfig(string command, string args)
        {
            ConfigWindow.Toggle();
        }

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            this.commandManager.Dispose();

            PluginInterface.SavePluginConfig(PluginConfig);

            PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
            PluginInterface.UiBuilder.OpenMainUi -= MainWindow.Toggle;
            PluginInterface.UiBuilder.OpenConfigUi -= ConfigWindow.Toggle;

            WindowSystem.RemoveAllWindows();

            ConfigWindow.Dispose();
            MainWindow.Dispose();
            ChangeNicknameWindow.Dispose();

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