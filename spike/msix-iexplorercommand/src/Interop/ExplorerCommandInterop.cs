using System;
using System.Runtime.InteropServices;

namespace SvgTools.ShellExtension.Interop
{
    // ─────────────────────────────────────────────────────────────────────────
    //  SPIKE: hand-written COM interop for the Windows 11 modern context menu.
    //
    //  These declarations mirror the native shell headers (ShObjIdl_core.h).
    //  They are written in the [PreserveSig] / explicit-HRESULT style so the
    //  handler can return specific HRESULTs (S_OK, S_FALSE, E_NOTIMPL) exactly
    //  as the shell expects — the CLR's default exception<->HRESULT translation
    //  is too lossy for GetState/EnumSubCommands.
    //
    //  NOT independently verified against a live shell. Validate marshalling
    //  (especially IShellItemArray enumeration) on Windows 11 before trusting.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>HRESULT constants used by shell command handlers.</summary>
    internal static class HResults
    {
        public const int S_OK = 0;
        public const int S_FALSE = 1;
        public const int E_NOTIMPL = unchecked((int)0x80004001);
        public const int E_FAIL = unchecked((int)0x80004005);
    }

    /// <summary>EXPCMDSTATE — enabled/disabled/hidden state of a command.</summary>
    [Flags]
    internal enum EXPCMDSTATE
    {
        ECS_ENABLED = 0x00,
        ECS_DISABLED = 0x01,
        ECS_HIDDEN = 0x02,
        ECS_CHECKBOX = 0x04,
        ECS_CHECKED = 0x08,
        ECS_RADIOCHECK = 0x10,
    }

    /// <summary>EXPCMDFLAGS — presentation flags (submenus, separators, …).</summary>
    [Flags]
    internal enum EXPCMDFLAGS
    {
        ECF_DEFAULT = 0x00,
        ECF_HASSUBCOMMANDS = 0x01,
        ECF_HASSPLITBUTTON = 0x02,
        ECF_HIDELABEL = 0x04,
        ECF_ISSEPARATOR = 0x08,
        ECF_HASLUASHIELD = 0x10,
        ECF_SEPARATORBEFORE = 0x20,
        ECF_SEPARATORAFTER = 0x40,
        ECF_ISDROPDOWN = 0x80,
    }

    /// <summary>
    /// IExplorerCommand — the modern shell command interface. A command that
    /// returns ECF_HASSUBCOMMANDS from GetFlags supplies children via
    /// EnumSubCommands, which is how nested flyouts are built.
    /// </summary>
    [ComImport]
    [Guid("a08ce4d0-fa25-44ab-b57c-c7b1c323e0b9")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IExplorerCommand
    {
        [PreserveSig] int GetTitle(IShellItemArray? psiItemArray, [MarshalAs(UnmanagedType.LPWStr)] out string? ppszName);
        [PreserveSig] int GetIcon(IShellItemArray? psiItemArray, [MarshalAs(UnmanagedType.LPWStr)] out string? ppszIcon);
        [PreserveSig] int GetToolTip(IShellItemArray? psiItemArray, [MarshalAs(UnmanagedType.LPWStr)] out string? ppszInfotip);
        [PreserveSig] int GetCanonicalName(out Guid pguidCommandName);
        [PreserveSig] int GetState(IShellItemArray? psiItemArray, [MarshalAs(UnmanagedType.Bool)] bool fOkToBeSlow, out EXPCMDSTATE pCmdState);
        [PreserveSig] int Invoke(IShellItemArray? psiItemArray, [MarshalAs(UnmanagedType.Interface)] object? pbc);
        [PreserveSig] int GetFlags(out EXPCMDFLAGS pFlags);
        [PreserveSig] int EnumSubCommands(out IEnumExplorerCommand? ppEnum);
    }

    /// <summary>IEnumExplorerCommand — enumerator over child commands.</summary>
    [ComImport]
    [Guid("a88826f8-186f-4987-aade-ea0cef8fbfe8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IEnumExplorerCommand
    {
        [PreserveSig] int Next(uint celt,
            [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Interface, SizeParamIndex = 0)] IExplorerCommand[] pUICommand,
            out uint pceltFetched);
        [PreserveSig] int Skip(uint celt);
        [PreserveSig] int Reset();
        [PreserveSig] int Clone(out IEnumExplorerCommand? ppenum);
    }

    /// <summary>
    /// IShellItemArray — the selected items passed to a command. For this
    /// handler we only need to walk it and pull the file-system path of each
    /// item (see ShellItemArrayExtensions.GetFilePaths).
    /// </summary>
    [ComImport]
    [Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItemArray
    {
        // Only the members this handler uses are declared; the rest of the
        // vtable is elided intentionally (declaring a partial IShellItemArray
        // is unsafe — the FULL vtable order must be preserved for real use).
        // TODO before real use: declare the complete interface in vtable order.
        [PreserveSig] int GetCount(out uint pdwNumItems);
        [PreserveSig] int GetItemAt(uint dwIndex, out IShellItem ppsi);
    }

    /// <summary>IShellItem — a single shell item; used to read its path.</summary>
    [ComImport]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItem
    {
        [PreserveSig] int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int GetParent(out IShellItem ppsi);
        [PreserveSig] int GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        [PreserveSig] int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        [PreserveSig] int Compare(IShellItem psi, uint hint, out int piOrder);
    }

    internal enum SIGDN : uint
    {
        SIGDN_FILESYSPATH = 0x80058000,
    }
}
