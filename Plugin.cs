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
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
        [PluginService] public static IClientState ClientState { get; set; } = null!;
        [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null!;

        private PluginCommandManager<Plugin> commandManager;
        public static Configuration PluginConfig { get; set; }

        public readonly WindowSystem WindowSystem = new("Nicknamer");
        private ConfigWindow ConfigWindow { get; init; }
        public static ChangeNicknameWindow ChangeNicknameWindow { get; set; }
        private MainWindow MainWindow { get; init; }

        public Plugin(IDalamudPluginInterface pluginInterface, IChatGui chat, ICommandManager commands, IClientState clientState)
        {
            PluginInterface = pluginInterface;
            Chat = chat;
            ClientState = clientState;

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
                //if (isHandled)
                //{
                //    return;
                //}
                if (!ClientState.IsLoggedIn) { return; }

                if (!PluginConfig.Nicknames.ContainsKey(ClientState.LocalContentId))
                {
                    PluginConfig.Nicknames.Add(ClientState.LocalContentId, new NicknameCollection());
                    PluginConfig.Save();
                }

                //TODO:
                //Have it be configurable for in front or behind player name
                //Make each NicknameCollection specific to character
                //Have universal custom color/in front or behind/italics and then have overrides for each person

                foreach (PlayerPayload CurrentPlayerPayload in sender.Payloads.Where(x => x is PlayerPayload))
                {
                    int NextIndex = sender.Payloads.FindIndex(x => x is RawPayload) - 1;

                    string PlayerName = CurrentPlayerPayload.PlayerName;
                    string PlayerWorld = CurrentPlayerPayload.World.Value.Name.ExtractText();

                    NicknameEntry? CurrentNicknameEntry = PluginConfig.Nicknames[ClientState.LocalContentId].Find(x => x.PlayerName == PlayerName && x.PlayerWorld == PlayerWorld);

                    if (CurrentNicknameEntry == null) { continue; }
                    if (CurrentNicknameEntry.Enabled == false) { continue; }
                    if (String.IsNullOrWhiteSpace(CurrentNicknameEntry.Nickname)) { return; }

                    //If we've set a global custom color but NOT an override
                    if (PluginConfig.Global_UseCustomColor && CurrentNicknameEntry.OverrideGlobalColor == false)
                    {
                        //Apply the color
                        sender.Payloads.Insert(NextIndex, new UIForegroundPayload(Plugin.PluginConfig.Global_SelectedColor)); NextIndex++;
                    }
                    //If we've set an override
                    if (CurrentNicknameEntry.OverrideGlobalColor)
                    {
                        //Apply the color
                        sender.Payloads.Insert(NextIndex, new UIForegroundPayload(CurrentNicknameEntry.OverrideGlobalColorActualColor)); NextIndex++;
                    }
                    //Insert the start of the new text payload
                    sender.Payloads.Insert(NextIndex, new TextPayload(" (")); NextIndex++;
                    //If we have global or override italics on
                    if (PluginConfig.Global_UseItalics || CurrentNicknameEntry.OverrideGlobalItalics)
                    {
                        //Apply italics
                        sender.Payloads.Insert(NextIndex, new EmphasisItalicPayload(true)); NextIndex++;
                    }
                    //Put the name in
                    sender.Payloads.Insert(NextIndex, new TextPayload(CurrentNicknameEntry.Nickname)); NextIndex++;
                    //If we have global or override italics on, end them here
                    if (PluginConfig.Global_UseItalics || CurrentNicknameEntry.OverrideGlobalItalics)
                    {
                        sender.Payloads.Insert(NextIndex, new EmphasisItalicPayload(false)); NextIndex++; 
                    }
                    //end of the text
                    sender.Payloads.Insert(NextIndex, new TextPayload(")")); NextIndex++;
                    //end the color
                    if (PluginConfig.Global_UseCustomColor || CurrentNicknameEntry.OverrideGlobalColor)
                    {
                        sender.Payloads.Insert(NextIndex, new UIForegroundPayload(0)); NextIndex++;
                    }
                    //}
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
                NicknameEntry? CurrentNicknameEntry = PluginConfig.Nicknames[ClientState.LocalContentId].Find(x => x.PlayerName == PlayerName && x.PlayerWorld == PlayerWorld);

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
            //ChangeNicknameWindow.StartingPositionX = addonContextMenu->X;
            //ChangeNicknameWindow.StartingPositionY = addonContextMenu->Y;
            ChangeNicknameWindow.OldNicknameString = "";
            NicknameEntry? currentNicknameEntry = PluginConfig.Nicknames[ClientState.LocalContentId].Find(x => x.PlayerName == PlayerName && x.PlayerWorld == PlayerWorld);

            if (currentNicknameEntry == null)
            {
                PluginConfig.Nicknames[ClientState.LocalContentId].Add(new NicknameEntry { PlayerName = PlayerName, PlayerWorld = PlayerWorld, Nickname = "", Enabled = true, ContentID = 0, OverrideGlobalItalics = false, OverrideGlobalStyle = false, OverrideGlobalColor = false, OverrideGlobalColorActualColor = 57 });
                PluginConfig.Save();
                currentNicknameEntry = PluginConfig.Nicknames[ClientState.LocalContentId].Find(x => x.PlayerName == PlayerName && x.PlayerWorld == PlayerWorld);
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
            //ChangeNicknameWindow.StartingPositionX = addonContextMenu->X;
            //ChangeNicknameWindow.StartingPositionY = addonContextMenu->Y;
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
            PluginConfig.Nicknames[ClientState.LocalContentId].Remove(currentNicknameEntry);
            PluginConfig.Nicknames[Plugin.ClientState.LocalContentId].Sort((a, b) => string.Compare(a.PlayerWorld, b.PlayerWorld, StringComparison.Ordinal));
            PluginConfig.Save();
            Chat.Print("[NN] " + currentNicknameEntry.PlayerName + "@" + currentNicknameEntry.PlayerWorld + "'s nickname has been removed.");
        }

        [Command("/nickname")]
        [Aliases("/nicknamer", "/nn")]
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
            PluginInterface.UiBuilder.OpenConfigUi -= ConfigWindow.Toggle;

            WindowSystem.RemoveAllWindows();

            ConfigWindow.Dispose();


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