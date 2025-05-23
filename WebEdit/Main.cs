﻿using Npp.DotNet.Plugin;
using System.Text;
using WebEdit.Properties;
using static Npp.DotNet.Plugin.Win32;
using static Npp.DotNet.Plugin.Winforms.WinGDI;
using static Npp.DotNet.Plugin.Winforms.WinUser;
using static System.Diagnostics.FileVersionInfo;

namespace WebEdit {
  partial class Main : IDotNetPlugin {
    /// <summary>See <see href="https://github.com/alex-ilin/WebEdit/blob/7bb4243/Legacy-v2.1/Src/Tags.ob2#L16"/></summary>
    public const int MaxKeyLen = 32;
    internal const string PluginName = "WebEdit";
    private const string MenuCmdPrefix = $"{PluginName} -";
    private const string IniFileName = PluginName + ".ini";
    private const string Version = "2.1";
    private static string MsgBoxCaption = $"{PluginName} {Version}";
    private const string AboutMsg =
      "This small freeware plugin allows you to wrap the selected text in "
      + "tag pairs and expand abbreviations using a hotkey.\n"
      + "For more information visit https://github.com/npp-dotnet/WebEdit\n"
      + "\n"
      + "Created by Alexander Iljin (Amadeus IT Solutions) using XDS Oberon, "
      + "March 2008 - March 2010.\n"
      + "Ported to C# by Miguel Febres, April 2021.\n"
      + "Ported to .NET 8 by Robert Di Pardo, February 2025.\n"
      + "Contact e-mail: AlexIljin@users.SourceForge.net";

    static IniFile ini = null;
    static bool isConfigDirty = false;
    internal static string iniDirectory, iniFilePath = null;

    public void OnBeNotified(ScNotification notification)
    {
      if (notification.Header.HwndFrom == PluginData.NppData.NppHandle)
      {
        uint code = notification.Header.Code;
        switch ((NppMsg)code)
        {
          case NppMsg.NPPN_READY:
          case NppMsg.NPPN_BUFFERACTIVATED:
          case NppMsg.NPPN_NATIVELANGCHANGED:
            SetMenuItemNames();
            break;
          case NppMsg.NPPN_FILESAVED:
            if (isConfigDirty &&
                  (string.Compare(iniFilePath, PluginData.Notepad.GetCurrentFilePath(), StringComparison.InvariantCultureIgnoreCase) == 0))
            {
              LoadConfig();
              isConfigDirty = false;
            }
            SetMenuItemNames();
            break;
          case NppMsg.NPPN_TBMODIFICATION:
            PluginData.FuncItems.RefreshItems();
            AddToolbarIcons();
            break;
          case NppMsg.NPPN_SHUTDOWN:
            PluginCleanUp();
            break;
        }
      }
    }

    /// <summary>
    /// Load the ini-file and initialize the menu items. The toolbar will be
    /// initialized later and will use the commands used in the menu added here
    /// to get the command identifiers for the toolbar buttons.
    /// </summary>
    public void OnSetInfo()
    {
      int i = 0;
      var npp = new NotepadPPGateway();
      iniDirectory = Path.Combine(npp.GetPluginConfigPath(), PluginName);
      _ = Directory.CreateDirectory(iniDirectory);
      iniFilePath = Path.Combine(iniDirectory, IniFileName);
      try
      {
        MsgBoxCaption =
          MsgBoxCaption.Replace(Version,
            GetVersionInfo(Path.Combine(npp.GetPluginsHomePath(), PluginName, $"{PluginName}.dll")).ProductVersion);
      }
      catch { }
      LoadConfig();
      var actions = new Actions(ini);
      foreach (string key in actions.iniKeys) {
        var methodInfo = actions.GetCommand(i++);
        if (methodInfo == null)
          break;

        Utils.SetCommand(
          $"{MenuCmdPrefix} {key}",
          () =>
          {
            var cmds = new Actions(ini);
            cmds.ExecuteCommand(ini.Get("Commands", key));
          });
      }
      Utils.SetCommand(
        "Replace Tag", ReplaceTag,
        new ShortcutKey(FALSE, TRUE, FALSE, 13));
      Utils.MakeSeparator();
      Utils.SetCommand("Edit Config", EditConfig);
      Utils.SetCommand("Load Config", LoadConfig);
      Utils.SetCommand("About...", About);
    }

    public NativeBool OnMessageProc(uint msg, UIntPtr wParam, IntPtr lParam) => TRUE;

    /// <summary>
    /// Edit the plugin ini-file in Notepad++.
    /// </summary>
    internal static void EditConfig()
    {
      if (!new NotepadPPGateway().OpenFile(iniFilePath))
        _ = MsgBoxDialog(
          PluginData.NppData.NppHandle,
          "Failed to open the configuration file for editing:\n" + iniFilePath,
          MsgBoxCaption,
          (uint)(MsgBox.ICONWARNING | MsgBox.OK));
      else
        isConfigDirty = true;
    }

    /// <summary>
    /// Load the settings from the ini-file. This is done on startup and when
    /// requested by the user via the Load Config menu. The iniFilePath member
    /// must be initialized prior to calling this method.
    /// </summary>
    internal static void LoadConfig()
    {
      if (!File.Exists(iniFilePath))
        using (var fs = File.Create(iniFilePath)) {
          byte[] info = new UTF8Encoding(true).GetBytes(Resources.WebEditIni);
          fs.Write(info, 0, info.Length);
        }
      // Reload the ini-file contents and update the menu and tag
      // replacement data.
      ini = new IniFile(iniFilePath);
    }

    /// <summary>
    /// Show the About message with copyright and version information.
    /// </summary>
    internal static void About()
      => _ = MsgBoxDialog(
            PluginData.NppData.NppHandle,
            AboutMsg,
            MsgBoxCaption,
            (uint)(MsgBox.ICONINFORMATION | MsgBox.OK));

    /// <summary>
    /// Add the toolbar icons for the menu items that have the configured
    /// bitmap files in the iniDirectory folder.
    /// </summary>
#pragma warning disable CS0618 // Dark mode unaware icons are deprecated since Npp v8.0
    internal static void AddToolbarIcons()
    {
      ToolbarIcon _tbIcons = default;
      ToolbarIconDarkMode tbIcons = default;
      bool hasDarkMode = PluginData.Notepad.GetNppVersion() switch { (int maj, _, _) => maj >= 8 };
      var actions = new Actions(ini);
      var icons = ini.GetKeys("Toolbar");
      for (int i = 0; i < actions.iniKeys.Length && i < icons.Length; ++i)
      {
        try
        {
          if (actions.GetCommand(i) == null)
            continue;
          MenuItemToToolbar(ini.Get("Toolbar", icons[i]).Replace("\0", ""), ref tbIcons);
          // The dark mode API requires at least one ICO, or else nothing will display
          if (hasDarkMode && tbIcons.HToolbarIcon != NULL)
            PluginData.Notepad.AddToolbarIcon(i, tbIcons);
          else
          {
            _tbIcons.HToolbarBmp = tbIcons.HToolbarBmp;
            _tbIcons.HToolbarIcon = tbIcons.HToolbarIcon;
            PluginData.Notepad.AddToolbarIcon(i, _tbIcons);
          }
        }
        catch
        {
          // Ignore any errors like missing or corrupt bitmap files, or
          // incorrect command index values.
        }
      }
    }
#pragma warning restore CS0618

    internal static void PluginCleanUp()
    {
      // This method is called when the plugin is notified about Npp shutdown.
      PluginData.PluginNamePtr = NULL;
      PluginData.FuncItems.Dispose();
    }

    /// <summary>
    /// Replace the tag at the caret with an expansion defined in the [Tags]
    /// ini-file section.
    /// </summary>
    internal static void ReplaceTag()
    {
      IntPtr currentScint = Utils.GetCurrentScintilla();
      ScintillaGateway scintillaGateway = new ScintillaGateway(currentScint);
      long lineCurrent = scintillaGateway.GetCurrentLineNumber();
      long lineStart = scintillaGateway.PositionFromLine(lineCurrent);
      long lineStartNext = scintillaGateway.PositionFromLine(lineCurrent + 1);
      string selectedText = scintillaGateway.GetSelText();

      if (string.IsNullOrEmpty(selectedText)) {
        string tag = PluginData.Notepad.GetCurrentWord();
        scintillaGateway.SetTargetRange(lineStart, lineStartNext);
        string lineText = scintillaGateway.GetTargetText();

        if (string.IsNullOrEmpty(lineText))
          return;

        long tagStartPos = lineText.IndexOf(tag, StringComparison.Ordinal);

        if (tagStartPos < 0)
          return;

        long selStart = lineStart + tagStartPos;
        long tagLength = scintillaGateway.CodePage.GetByteCount(tag);
        long selEnd = selStart + tagLength;
        scintillaGateway.SetSelection(selStart, selEnd);
        selectedText = scintillaGateway.GetSelText();
      }

      long position = scintillaGateway.GetSelectionEnd();
      scintillaGateway.BeginUndoAction();
      try {
        if (string.IsNullOrEmpty(selectedText?.Trim())) {
          position = scintillaGateway.GetCurrentPos();
          scintillaGateway.ClearSelectionToCursor();
          scintillaGateway.CallTipShow(position, "No tag here.");
          return;
        }
        else if (selectedText.Length > MaxKeyLen) {
          scintillaGateway.CallTipShow(position, $"Maximum tag length is {MaxKeyLen} characters.");
          return;
        }

        LoadConfig();
        string value = ini.Get("Tags", selectedText);

        if (string.IsNullOrEmpty(value.Trim('\0'))) {
          scintillaGateway.CallTipShow(position, $"Undefined tag: {selectedText}");
          return;
        }

        long selStart = scintillaGateway.GetSelectionStart();
        long indentPos = Math.Min(scintillaGateway.GetLineIndentPosition(lineStart), selStart);
        Tags parser = new(lineStart, indentPos);
        value = parser.Unescape(value);
        scintillaGateway.ReplaceSel(Tags.UserDefinedInsertionPoint().Replace(value, string.Empty));

        if (parser.FindAndReplace(selStart) || !Tags.UserDefinedInsertionPoint().IsMatch(value))
          return;

        scintillaGateway.SetSelectionEnd(position + value.Substring(0, value.IndexOf('|')).Length - selectedText.Length);
      } catch (Exception ex) {
        scintillaGateway.CallTipShow(position, ex.Message);
      }
      scintillaGateway.EndUndoAction();
    }

    /// <summary>
    /// Parse a delimited string of 1-3 icon file names, load the icon files
    /// and assign their handles to the given <paramref name="tbIcons"/> instance.
    /// </summary>
    private static void MenuItemToToolbar(string iniValueString, ref ToolbarIconDarkMode tbIcons)
    {
      string[] icons = iniValueString.Split(IniFile.ValueStringDelimiter, StringSplitOptions.RemoveEmptyEntries);
      for (int i = 0; i < icons.Length; ++i)
      {
        string iconFileName = icons[i]?.Trim().ToLowerInvariant();
        string iconExt = Path.GetExtension(iconFileName)?.ToLowerInvariant();
        if (iconExt == ".bmp")
          LoadToolbarIcon(LoadImageType.IMAGE_BITMAP, GetIconPath(iconFileName), out tbIcons.HToolbarBmp);
        else if (iconExt == ".ico")
        {
          if (i == 1)
          {
            LoadToolbarIcon(LoadImageType.IMAGE_ICON, GetIconPath(iconFileName), out tbIcons.HToolbarIcon);
            if (icons.Length < 3)
              tbIcons.HToolbarIconDarkMode = tbIcons.HToolbarIcon;
          }
          if (i == 2)
            LoadToolbarIcon(LoadImageType.IMAGE_ICON, GetIconPath(iconFileName), out tbIcons.HToolbarIconDarkMode);
        }
      }
    }

    /// <summary>
    /// Load a bitmap or icon from the given file name and return the handle.
    /// </summary>
    private static void LoadToolbarIcon(LoadImageType imgType, string iconFile, out IntPtr hImg)
    {
      var loadFlags = LoadImageFlag.LR_LOADFROMFILE;
      switch (imgType)
      {
        case LoadImageType.IMAGE_BITMAP:
          (int bmpX, int bmpY) = GetLogicalPixels(16, 16);
          hImg = LoadImage(NULL, iconFile, imgType, bmpX, bmpY, loadFlags | LoadImageFlag.LR_LOADMAP3DCOLORS);
          break;
        case LoadImageType.IMAGE_ICON:
          (int icoX, int icoY) = GetLogicalPixels(32, 32);
          hImg = LoadImage(NULL, iconFile, imgType, icoX, icoY, loadFlags | LoadImageFlag.LR_LOADTRANSPARENT);
          break;
        default:
          hImg = NULL;
          break;
      }
    }

    /// <summary>
    /// Return the absolute path to an icon file. The user's config directory
    /// is tried first; then the plugin's installation folder.
    /// </summary>
    private static string GetIconPath(string icon)
    {
      string path = Path.Combine(PluginData.Notepad.GetPluginConfigPath(), PluginName, icon);
      if (!File.Exists(path))
        path = Path.Combine(PluginData.Notepad.GetPluginsHomePath(), PluginName, "Config", icon);
      return path;
    }

    /// <summary>
    /// Set the text of each menu item, i.e., remove the &quot;WebEdit -  &quot; prefix.
    /// </summary>
    /// <remarks>
    /// Adapted from <see href="https://github.com/alex-ilin/WebEdit/blob/7bb4243/Legacy-v2.1/Src/NotepadPPU.ob2#L184"/>
    /// </remarks>
    private static unsafe void SetMenuItemNames()
    {
      Actions actions = new(ini);
      IntPtr hMenu = SendMessage(PluginData.NppData.NppHandle, (uint)NppMsg.NPPM_GETMENUHANDLE, (uint)NppMsg.NPPPLUGINMENU);
      for (int i = 0; i < actions.iniKeys.Length && i < PluginData.FuncItems.Items.Count; ++i)
      {
        try
        {
          var itemName = actions.iniKeys[i];
          var itemID = PluginData.FuncItems.Items[i].CmdID;
          fixed (char* lpNewItem = itemName)
          {
            ModifyMenu(hMenu, itemID, MF_BYCOMMAND | MF_STRING, (UIntPtr)itemID, (IntPtr)lpNewItem);
          }
        }
        catch { }
      }
    }
  }
}
