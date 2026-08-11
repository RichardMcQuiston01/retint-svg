using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SvgTools.ShellExtension.Interop;

namespace SvgTools.ShellExtension
{
    // ─────────────────────────────────────────────────────────────────────────
    //  SPIKE: abstract base that implements the repetitive parts of
    //  IExplorerCommand so concrete commands only declare a title, flags, and
    //  (for leaves) what to do on Invoke.
    // ─────────────────────────────────────────────────────────────────────────

    internal abstract class ExplorerCommandBase : IExplorerCommand
    {
        /// <summary>The label shown in the menu.</summary>
        protected abstract string Title { get; }

        /// <summary>Presentation flags — override to declare submenus/separators.</summary>
        protected virtual EXPCMDFLAGS Flags => EXPCMDFLAGS.ECF_DEFAULT;

        /// <summary>Child commands, if this is a flyout. Empty for leaves.</summary>
        protected virtual IReadOnlyList<ExplorerCommandBase> SubCommands => Array.Empty<ExplorerCommandBase>();

        /// <summary>Optional icon resource path (e.g. "C:\…\swatch.ico").</summary>
        protected virtual string? IconResource => null;

        /// <summary>Leaf action. Receives the selected file-system paths.</summary>
        protected virtual void Execute(IReadOnlyList<string> selectedPaths) { }

        // ── IExplorerCommand ─────────────────────────────────────────────────

        public int GetTitle(IShellItemArray? psiItemArray, out string? ppszName)
        {
            ppszName = Title;
            return HResults.S_OK;
        }

        public int GetIcon(IShellItemArray? psiItemArray, out string? ppszIcon)
        {
            ppszIcon = IconResource;
            return IconResource is null ? HResults.S_FALSE : HResults.S_OK;
        }

        public int GetToolTip(IShellItemArray? psiItemArray, out string? ppszInfotip)
        {
            ppszInfotip = null;
            return HResults.E_NOTIMPL; // shell falls back to no tooltip
        }

        public int GetCanonicalName(out Guid pguidCommandName)
        {
            pguidCommandName = Guid.Empty;
            return HResults.E_NOTIMPL;
        }

        public int GetState(IShellItemArray? psiItemArray, bool fOkToBeSlow, out EXPCMDSTATE pCmdState)
        {
            pCmdState = EXPCMDSTATE.ECS_ENABLED;
            return HResults.S_OK;
        }

        public int Invoke(IShellItemArray? psiItemArray, object? pbc)
        {
            try
            {
                var paths = ShellItemArrayExtensions.GetFilePaths(psiItemArray);
                Execute(paths);
                return HResults.S_OK;
            }
            catch
            {
                // A shell command must never throw across the COM boundary.
                return HResults.E_FAIL;
            }
        }

        public int GetFlags(out EXPCMDFLAGS pFlags)
        {
            pFlags = Flags;
            return HResults.S_OK;
        }

        public int EnumSubCommands(out IEnumExplorerCommand? ppEnum)
        {
            var subs = SubCommands;
            if (subs.Count == 0)
            {
                ppEnum = null;
                return HResults.E_NOTIMPL;
            }

            var array = new IExplorerCommand[subs.Count];
            for (int i = 0; i < subs.Count; i++) array[i] = subs[i];
            ppEnum = new ExplorerCommandEnumerator(array);
            return HResults.S_OK;
        }
    }

    /// <summary>Minimal IEnumExplorerCommand over a fixed array of commands.</summary>
    internal sealed class ExplorerCommandEnumerator : IEnumExplorerCommand
    {
        private readonly IExplorerCommand[] _commands;
        private int _index;

        public ExplorerCommandEnumerator(IExplorerCommand[] commands) => _commands = commands;

        public int Next(uint celt, IExplorerCommand[] pUICommand, out uint pceltFetched)
        {
            uint fetched = 0;
            while (fetched < celt && _index < _commands.Length)
                pUICommand[fetched++] = _commands[_index++];

            pceltFetched = fetched;
            return fetched == celt ? HResults.S_OK : HResults.S_FALSE;
        }

        public int Skip(uint celt) { _index += (int)celt; return HResults.S_OK; }
        public int Reset() { _index = 0; return HResults.S_OK; }

        public int Clone(out IEnumExplorerCommand? ppenum)
        {
            ppenum = new ExplorerCommandEnumerator(_commands) { _index = _index };
            return HResults.S_OK;
        }
    }

    /// <summary>Walks an IShellItemArray and returns each item's file path.</summary>
    internal static class ShellItemArrayExtensions
    {
        public static IReadOnlyList<string> GetFilePaths(IShellItemArray? array)
        {
            var paths = new List<string>();
            if (array is null) return paths;

            if (array.GetCount(out uint count) != HResults.S_OK) return paths;

            for (uint i = 0; i < count; i++)
            {
                if (array.GetItemAt(i, out IShellItem item) != HResults.S_OK || item is null)
                    continue;

                if (item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out string path) == HResults.S_OK
                    && !string.IsNullOrEmpty(path))
                {
                    paths.Add(path);
                }

                Marshal.ReleaseComObject(item);
            }

            return paths;
        }
    }
}
