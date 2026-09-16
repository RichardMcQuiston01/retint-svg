using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ImageTools.Core;

namespace SVGToolsShell
{
    /// <summary>
    /// A PowerToys-style batch rename dialog for the selected image files:
    /// search/replace (literal or regex), case and scope options, an optional
    /// <c>${n}</c> counter, and a live preview. The matching logic is in
    /// <see cref="RenameEngine"/> (Core, unit-tested); this dialog collects
    /// options, shows the preview with conflict flags, and performs the moves.
    /// </summary>
    internal sealed class PowerRenameDialog : Form
    {
        private readonly string[] _sources;      // full paths, selection order
        private string[] _targets;               // full target paths, per row
        private bool[] _willRename;              // changed AND not conflicting

        private readonly TextBox _search = new TextBox();
        private readonly TextBox _replace = new TextBox();
        private readonly CheckBox _regex = new CheckBox();
        private readonly CheckBox _caseSensitive = new CheckBox();
        private readonly CheckBox _allOccurrences = new CheckBox();
        private readonly ComboBox _target = new ComboBox();
        private readonly NumericUpDown _start = new NumericUpDown();
        private readonly NumericUpDown _pad = new NumericUpDown();
        private readonly ListView _preview = new ListView();
        private readonly Label _status = new Label();
        private readonly Button _ok = new Button();

        public PowerRenameDialog(IReadOnlyList<string> sourcePaths)
        {
            _sources = new string[sourcePaths.Count];
            for (var i = 0; i < sourcePaths.Count; i++) _sources[i] = sourcePaths[i];
            _targets = new string[_sources.Length];
            _willRename = new bool[_sources.Length];

            Text = "Power Rename";
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowInTaskbar = false;
            ClientSize = new Size(560, 460);
            MinimumSize = new Size(520, 420);
            Font = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;

            var lblSearch = new Label { Text = "Search:", Location = new Point(12, 15), Size = new Size(70, 20) };
            _search.Location = new Point(88, 12);
            _search.Size = new Size(300, 23);
            _search.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            var lblReplace = new Label { Text = "Replace:", Location = new Point(12, 45), Size = new Size(70, 20) };
            _replace.Location = new Point(88, 42);
            _replace.Size = new Size(300, 23);
            _replace.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            _regex.Text = "Use regular expressions";
            _regex.Location = new Point(88, 72);
            _regex.Size = new Size(170, 22);

            _caseSensitive.Text = "Case sensitive";
            _caseSensitive.Location = new Point(262, 72);
            _caseSensitive.Size = new Size(120, 22);

            _allOccurrences.Text = "All occurrences";
            _allOccurrences.Checked = true;
            _allOccurrences.Location = new Point(88, 96);
            _allOccurrences.Size = new Size(140, 22);

            var lblApply = new Label { Text = "Apply to:", Location = new Point(262, 98), Size = new Size(60, 20) };
            _target.DropDownStyle = ComboBoxStyle.DropDownList;
            _target.Items.AddRange(new object[] { "Name only", "Extension only", "Name + extension" });
            _target.SelectedIndex = 0;
            _target.Location = new Point(326, 95);
            _target.Size = new Size(130, 23);

            var lblStart = new Label { Text = "Counter start:", Location = new Point(88, 128), Size = new Size(90, 20) };
            _start.Location = new Point(180, 125);
            _start.Size = new Size(60, 23);
            _start.Minimum = 0;
            _start.Maximum = 1000000;
            _start.Value = 1;

            var lblPad = new Label { Text = "Pad:", Location = new Point(252, 128), Size = new Size(34, 20) };
            _pad.Location = new Point(288, 125);
            _pad.Size = new Size(45, 23);
            _pad.Minimum = 0;
            _pad.Maximum = 8;

            var lblHint = new Label
            {
                Text = "Tip: put ${n} in Replace for an incrementing number.",
                Location = new Point(12, 152),
                Size = new Size(536, 18),
                ForeColor = SystemColors.GrayText,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };

            _preview.View = View.Details;
            _preview.FullRowSelect = true;
            _preview.GridLines = true;
            _preview.MultiSelect = false;
            _preview.Location = new Point(12, 174);
            _preview.Size = new Size(536, 232);
            _preview.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _preview.Columns.Add("Original", 255);
            _preview.Columns.Add("New name", 255);

            _status.Location = new Point(12, 414);
            _status.Size = new Size(360, 32);
            _status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            _ok.Text = "Rename";
            _ok.Location = new Point(384, 420);
            _ok.Size = new Size(78, 26);
            _ok.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _ok.Click += (_, __) => ExecuteRename();

            var cancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(468, 420),
                Size = new Size(78, 26),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            };

            Controls.AddRange(new Control[]
            {
                lblSearch, _search, lblReplace, _replace,
                _regex, _caseSensitive, _allOccurrences,
                lblApply, _target, lblStart, _start, lblPad, _pad, lblHint,
                _preview, _status, _ok, cancel,
            });

            CancelButton = cancel;

            // Live preview on any change.
            _search.TextChanged += OnOptionChanged;
            _replace.TextChanged += OnOptionChanged;
            _regex.CheckedChanged += OnOptionChanged;
            _caseSensitive.CheckedChanged += OnOptionChanged;
            _allOccurrences.CheckedChanged += OnOptionChanged;
            _target.SelectedIndexChanged += OnOptionChanged;
            _start.ValueChanged += OnOptionChanged;
            _pad.ValueChanged += OnOptionChanged;

            RefreshPreview();
        }

        private void OnOptionChanged(object? sender, EventArgs e) => RefreshPreview();

        private RenameOptions BuildOptions() => new RenameOptions
        {
            Search = _search.Text,
            Replace = _replace.Text,
            UseRegex = _regex.Checked,
            CaseSensitive = _caseSensitive.Checked,
            MatchAllOccurrences = _allOccurrences.Checked,
            Target = (RenameTarget)_target.SelectedIndex,
            CounterStart = (int)_start.Value,
            CounterPadding = (int)_pad.Value,
        };

        private void RefreshPreview()
        {
            var names = new string[_sources.Length];
            for (var i = 0; i < _sources.Length; i++) names[i] = Path.GetFileName(_sources[i]);

            IReadOnlyList<RenameResult> plan;
            try
            {
                plan = RenameEngine.Plan(names, BuildOptions());
            }
            catch (ArgumentException ex)
            {
                _preview.Items.Clear();
                _status.Text = ex.Message;
                _status.ForeColor = Color.Firebrick;
                _ok.Enabled = false;
                return;
            }

            // Resolve target full paths and flag conflicts (duplicate targets, or a
            // target that already exists on disk and isn't the file's own source).
            var conflict = new bool[_sources.Length];
            var targetCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < _sources.Length; i++)
            {
                var dir = Path.GetDirectoryName(_sources[i]) ?? string.Empty;
                _targets[i] = dir.Length == 0 ? plan[i].Proposed : Path.Combine(dir, plan[i].Proposed);
                if (plan[i].Changed)
                    targetCounts[_targets[i]] = targetCounts.TryGetValue(_targets[i], out var c) ? c + 1 : 1;
            }

            _preview.BeginUpdate();
            _preview.Items.Clear();
            var willRenameCount = 0;
            for (var i = 0; i < _sources.Length; i++)
            {
                var changed = plan[i].Changed;
                var dupTarget = changed && targetCounts.TryGetValue(_targets[i], out var c) && c > 1;
                var existsOnDisk = changed
                    && !string.Equals(_targets[i], _sources[i], StringComparison.OrdinalIgnoreCase)
                    && SafeExists(_targets[i]);
                conflict[i] = dupTarget || existsOnDisk;
                _willRename[i] = changed && !conflict[i];
                if (_willRename[i]) willRenameCount++;

                var row = new ListViewItem(plan[i].Original);
                row.SubItems.Add(conflict[i] ? plan[i].Proposed + "  (conflict)" : plan[i].Proposed);
                if (conflict[i]) row.ForeColor = Color.Firebrick;
                else if (!changed) row.ForeColor = SystemColors.GrayText;
                _preview.Items.Add(row);
            }
            _preview.EndUpdate();

            var conflicts = 0;
            foreach (var b in conflict) if (b) conflicts++;
            _status.ForeColor = conflicts > 0 ? Color.Firebrick : SystemColors.ControlText;
            _status.Text = conflicts > 0
                ? $"{willRenameCount} to rename, {conflicts} skipped (name conflict)."
                : $"{willRenameCount} to rename.";
            _ok.Enabled = willRenameCount > 0;
        }

        private void ExecuteRename()
        {
            var renamed = 0;
            var errors = new List<string>();
            for (var i = 0; i < _sources.Length; i++)
            {
                if (!_willRename[i]) continue;
                try
                {
                    // File.Move never overwrites; a target that appeared since the
                    // preview throws and is reported rather than clobbering.
                    File.Move(_sources[i], _targets[i]);
                    renamed++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{Path.GetFileName(_sources[i])}: {ex.Message}");
                }
            }

            if (errors.Count > 0)
            {
                MessageBox.Show(
                    $"Renamed {renamed} file(s).\n\nThe following could not be renamed:\n"
                        + string.Join("\n", errors),
                    "Power Rename", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private static bool SafeExists(string path)
        {
            try { return File.Exists(path); } catch { return false; }
        }
    }
}
