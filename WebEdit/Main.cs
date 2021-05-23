﻿using Kbg.NppPluginNET.PluginInfrastructure;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using WebEdit;
using WebEdit.IniFiles;
using WebEdit.Properties;

namespace Kbg.NppPluginNET {
  class Main {
    internal const string PluginName = "WebEdit";
    private const string IniFileName = PluginName + ".ini";
    private const string Version = "2.1";
    private const string MsgBoxCaption = PluginName + " " + Version;
    private const string AboutMsg =
      "This small freeware plugin allows you to wrap the selected text in "
      + "tag pairs and expand abbreviations using a hotkey.\n"
      + "For more information refer to " + PluginName + ".txt.\n"
      + "\n"
      + "Created by Alexander Iljin (Amadeus IT Solutions) using XDS Oberon, "
      + "March 2008 - March 2010.\n"
      + "Contact e-mail: AlexIljin@users.SourceForge.net";

    static string iniDirectory, iniFilePath = null;

    public static void OnNotification(ScNotification notification)
    {
    }

    /// <summary>
    /// Load Menu section
    /// </summary>
    internal static void CommandMenuInit()
    {
      int i = 0;
      var npp = new NotepadPPGateway();
      iniDirectory = npp.GetPluginConfigPath();
      if (!Directory.Exists(iniDirectory)) {
        Directory.CreateDirectory(iniDirectory);
      }
      iniFilePath = Path.Combine(iniDirectory, IniFileName);
      LoadConfig();
      // TODO: move the menu initialization to the LoadConfig method.
      var ini = new IniFile(iniFilePath);
      var actions = new Actions(ini);
      foreach (string key in actions.iniKeys) {
        var methodInfo = typeof(Actions).GetMethod("ExecuteCommand" + i);
        if (methodInfo == null)
          break;

        PluginBase.SetCommand(
          i++,
          key,
          (NppFuncItemDelegate) Delegate.CreateDelegate(
            typeof(NppFuncItemDelegate), actions, methodInfo.Name));
      }
      PluginBase.SetCommand(
        i++, "Replace Tag", ReplaceTag,
        new ShortcutKey(false, true, false, Keys.Enter));
      PluginBase.SetCommand(0, "", null);
      PluginBase.SetCommand(i++, "Edit Config", EditConfig);
      PluginBase.SetCommand(i++, "Load Config", LoadConfig);
      PluginBase.SetCommand(i++, "About...", About);
    }

    /// <summary>
    /// Edit the plugin ini-file in Notepad++.
    /// </summary>
    internal static void EditConfig()
    {
      if (!new NotepadPPGateway().OpenFile(iniFilePath))
        _ = MessageBox.Show(
          "Failed to open the configuration file for editing:\n" + iniFilePath,
          MsgBoxCaption);
    }

    /// <summary>
    /// Load the settings from the ini-file. This is done on startup and when
    /// requested by the user via the Load Config menu. The iniFilePath member
    /// must be initialized prior to calling this method.
    /// </summary>
    internal static unsafe void LoadConfig()
    {
      if (!File.Exists(iniFilePath))
        using (var fs = File.Create(iniFilePath)) {
          byte[] info = new UTF8Encoding(true).GetBytes(Resources.WebEditIni);
          fs.Write(info, 0, info.Length);
        }
      // TODO: load the ini-file contents and update the menu and tag
      // replacement data.
    }

    /// <summary>
    /// Show internal information.
    /// </summary>
    internal static void About()
      => _ = MessageBox.Show(AboutMsg, MsgBoxCaption);

    /// <summary>
    /// Set toolbar icons.
    /// </summary>
    internal static void SetToolBarIcon()
    {
      if (File.Exists(iniFilePath)) {
        toolbarIcons tbIcons = new toolbarIcons();
        var ini = new IniFile(iniFilePath);
        foreach (string key in ini.GetKeys("Toolbar")) {
          string value = ini.Get("Toolbar", key);
          var pathIcon = Path.Combine(iniDirectory, PluginName, value.Trim('\0').Replace("\0", ""));
          if (File.Exists(pathIcon)) {
            try {
              Bitmap icon = new Bitmap(pathIcon);
              tbIcons.hToolbarBmp = icon.GetHbitmap();
              IntPtr pTbIcons = Marshal.AllocHGlobal(Marshal.SizeOf(tbIcons));
              Marshal.StructureToPtr(tbIcons, pTbIcons, false);
              Win32.SendMessage(PluginBase.nppData._nppHandle, (uint) NppMsg.NPPM_ADDTOOLBARICON,
                  PluginBase._funcItems.Items[Convert.ToInt32(key) - 1]._cmdID, pTbIcons);
              Marshal.FreeHGlobal(pTbIcons);
            } catch {

            }
          }
        }
      }
    }

    internal static void PluginCleanUp()
    {
      // This method is called when the plugin is notified about Npp shutdown.
    }

    /// <summary>
    /// Replace the tag at the caret with an expansion defined in the [Tags]
    /// ini-file section.
    /// </summary>
    internal static void ReplaceTag()
    {
      IntPtr currentScint = PluginBase.GetCurrentScintilla();
      ScintillaGateway scintillaGateway = new ScintillaGateway(currentScint);
      int position = scintillaGateway.GetSelectionEnd();

      string selectedText = scintillaGateway.GetSelText();
      if (string.IsNullOrEmpty(selectedText)) {
        // TODO: remove this hardcoded 10 crap. Remove selection manipulation:
        // user will not be happy to see any such side-effects.
        scintillaGateway.SetSelection(position > 10 ? (position - 10) : (position - position), position);
        selectedText = scintillaGateway.GetSelText();
        var reges = Regex.Matches(scintillaGateway.GetSelText(), @"(\w+)");
        if (reges.Count > 0) {
          selectedText = reges.Cast<Match>().Select(m => m.Value).LastOrDefault();
          scintillaGateway.SetSelection(position - selectedText.Length, position);
          selectedText = scintillaGateway.GetSelText();
        }
      }
      try {
        if (string.IsNullOrEmpty(selectedText)) {
          throw new Exception("No tag here.");
        }
        byte[] buffer = new byte[1048];
        var ini = new IniFile(iniFilePath);
        string value = ini.Get("Tags", selectedText, 1048);
        if (string.IsNullOrEmpty(value.Trim('\0'))) {
          throw new Exception("No tag here.");
        }
        value = TransformTags(value);
        scintillaGateway.ReplaceSel(value.Replace("|", null));
        scintillaGateway.SetSelectionEnd(position + value.Substring(0, value.IndexOf('|')).Length - selectedText.Length);
      } catch (Exception ex) {
        scintillaGateway.CallTipShow(position, ex.Message);
      }
    }

    /// <summary>
    /// Transform string to uncode
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    private static string TransformTags(string value)
    {
      // TODO: add more commands: \\, \t.
      // TODO: does indentation work? I don't see insertions before \n.
      value = value.Replace("\\n", "\n");
      if (value.Contains("\\c")) {
        // TODO: what the heck is this? It's supposed to insert text from the
        // system Clipboard.
        value = value.Replace("\\c", "ScintillaGateway scintillaGateway = new ScintillaGateway(currentScint)");
      }
      value = value.Replace("\\i", "  ");
      return value;
    }

  }
}
