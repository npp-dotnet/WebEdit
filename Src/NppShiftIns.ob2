<*+main*> (* This marks the main module of a program or library.               *)
<*heaplimit="10000"*> (* Maximum heap size should be set in the main module,
because the changes do not take effect until the main module is recompiled.    *)
MODULE NppShiftIns;

(* ---------------------------------------------------------------------------
 * (C) 2008 by Alexander Iljin
 * --------------------------------------------------------------------------- *)

IMPORT
   SYSTEM,Win:=Windows;

(* ---------------------------------------------------------------------------
 * This is a simple Notepad++ plugin (XDS Oberon module). It adds the standard
 * Ctrl+INS, Shift+INS and Shift+DEL key combinations to both Scintilla
 * components of the Notepad++ on startup. Recommended for Notepad++ 4.8 and
 * later (version 4.8 is the first one that would not support those shortcuts
 * out of the box).
 * Known issue: Shortcut Mapper does not preserve the shortcuts.
 * --------------------------------------------------------------------------- *)

CONST
   PluginName = 'NppShiftIns';
   (* Menu items *)
   RegisterStr = 'Register shortcuts';
   AboutStr = 'About...';

   AboutMsg = 'This is a freeware plugin for Notepad++ v.4.8 and later.'+0DX+0AX
      +'This small plugin adds the standard Ctrl+INS, Shift+INS and Shift+DEL'
         +' key combinations to Scintilla on startup.'+0DX+0AX
      +0DX+0AX
      +'Known problem: the Shortcut Mapper does not preserve shortcuts registered by this plugin.'+0DX+0AX
      +"After you've used the Shortcut Mapper you may want to manually register those again using menu:"+0DX+0AX
      +'   Plugins -> '+PluginName+' -> '+RegisterStr+0DX+0AX
      +'or simply restart Notepad++.'+0DX+0AX
      +0DX+0AX
      +'Created by Alexander Iljin (Amadeus IT Solutions) using XDS Oberon, 28 Feb 2007.';

   (* Scintilla keyboard constants *)
   SCK_DELETE = 308;
   SCK_INSERT = 309;
   SCMOD_SHIFT = 1;
   SCMOD_CTRL = 2;

   (* Scintilla command codes *)
   SCI_ASSIGNCMDKEY = 2070;
   SCI_CUT = 2177;
   SCI_COPY = 2178;
   SCI_PASTE = 2179;
   
   (* Notepad++ notification codes *)
   NPPN_FIRST = 1000;
   NPPN_READY = NPPN_FIRST + 1;

TYPE
   Shortcut = POINTER TO ShortcutDesc;
   ShortcutDesc = RECORD
      ctrl : BOOLEAN;
      alt  : BOOLEAN;
      shift: BOOLEAN;
      key  : CHAR;
   END;

   FuncItem = RECORD
      itemName: ARRAY 64 OF CHAR;
      pFunc   : PROCEDURE ['C'];
      cmdID   : LONGINT;
      initChk : LONGINT;
      shortcut: Shortcut;
   END;

   SCNotification = RECORD
      nmhdr: Win.NMHDR;
      (* other fields are not used in this plugin *)
   END;

VAR
   nppHandle: Win.HWND;
   scintillaMainHandle: Win.HWND;
   scintillaSecondHandle: Win.HWND;
   FI: ARRAY 2 OF FuncItem;

PROCEDURE RegisterHotkeys (scintillaHandle: Win.HWND);
BEGIN
   Win.SendMessage (scintillaHandle, SCI_ASSIGNCMDKEY, SCMOD_SHIFT * 65536 + SCK_DELETE, SCI_CUT);
   Win.SendMessage (scintillaHandle, SCI_ASSIGNCMDKEY, SCMOD_CTRL  * 65536 + SCK_INSERT, SCI_COPY);
   Win.SendMessage (scintillaHandle, SCI_ASSIGNCMDKEY, SCMOD_SHIFT * 65536 + SCK_INSERT, SCI_PASTE);
END RegisterHotkeys;

PROCEDURE RegisterAll ();
BEGIN
   RegisterHotkeys (scintillaMainHandle);
   RegisterHotkeys (scintillaSecondHandle);
END RegisterAll;

PROCEDURE ['C'] Register ();
BEGIN
   RegisterAll;
END Register;

PROCEDURE ['C'] About ();
BEGIN
   Win.MessageBox (nppHandle, AboutMsg, PluginName, Win.MB_OK);
END About;

PROCEDURE ['C'] setInfo* (npp, scintillaMain, scintillaSecond: Win.HWND);
BEGIN
   nppHandle := npp;
   scintillaMainHandle := scintillaMain;
   scintillaSecondHandle := scintillaSecond;
END setInfo;

PROCEDURE ['C'] getName* (): Win.PCHAR;
BEGIN
   RETURN SYSTEM.VAL (Win.PCHAR, SYSTEM.ADR (PluginName));
END getName;

PROCEDURE ['C'] beNotified* (VAR note: SCNotification);
BEGIN
   IF (note.nmhdr.hwndFrom = nppHandle) & (note.nmhdr.code = NPPN_READY) THEN
      RegisterAll;
   END
END beNotified;

PROCEDURE ['C'] messageProc* (msg: Win.UINT; wParam: Win.WPARAM; lParam: Win.LPARAM): Win.LRESULT;
BEGIN
   RETURN 0
END messageProc;

PROCEDURE ['C'] getFuncsArray* (VAR nFuncs: LONGINT): LONGINT;
BEGIN
   nFuncs := LEN (FI);
   RETURN SYSTEM.ADR (FI);
END getFuncsArray;

PROCEDURE Init ();
BEGIN
   COPY (RegisterStr, FI [0].itemName);
   FI [0].pFunc := Register;
   FI [0].cmdID := 0;
   FI [0].initChk := 0;
   FI [0].shortcut := NIL;

   COPY (AboutStr, FI [1].itemName);
   FI [1].pFunc := About;
   FI [1].cmdID := 0;
   FI [1].initChk := 0;
   FI [1].shortcut := NIL;
END Init;

BEGIN
   Init;
END NppShiftIns.
