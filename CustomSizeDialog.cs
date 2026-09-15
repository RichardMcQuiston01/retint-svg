using System;
using System.Drawing;
using System.Windows.Forms;
using ImageTools.Core;

namespace SVGToolsShell
{
    /// <summary>
    /// A small modal dialog for the "Custom…" entry of the Resize Images menu.
    /// Lets the user pick one of the three <see cref="SizeKind"/> modes — a
    /// percentage, a longest-edge pixel count, or an exact width×height — and
    /// whether images smaller than the target may be enlarged. On OK it exposes
    /// the chosen <see cref="Spec"/> and <see cref="AllowUpscale"/>; the caller
    /// feeds those into the same worker-launch path the presets use.
    ///
    /// Kept in the net48 handler project (not Core) because it depends on
    /// System.Windows.Forms; Core stays UI-agnostic.
    /// </summary>
    internal sealed class CustomSizeDialog : Form
    {
        private readonly RadioButton _rbPercent = new RadioButton();
        private readonly RadioButton _rbLongest = new RadioButton();
        private readonly RadioButton _rbExact   = new RadioButton();

        private readonly NumericUpDown _numPercent = new NumericUpDown();
        private readonly NumericUpDown _numLongest = new NumericUpDown();
        private readonly NumericUpDown _numWidth   = new NumericUpDown();
        private readonly NumericUpDown _numHeight  = new NumericUpDown();

        private readonly CheckBox _chkUpscale = new CheckBox();

        /// <summary>The size the user chose. Valid only when ShowDialog returned OK.</summary>
        public SizeSpec Spec { get; private set; } = SizeSpec.FromPercent(50);

        /// <summary>Whether enlarging is permitted. Valid only when ShowDialog returned OK.</summary>
        public bool AllowUpscale { get; private set; } = true;

        public CustomSizeDialog()
        {
            Text = "Resize Images — Custom size";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(380, 214);
            Font = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;

            var group = new GroupBox
            {
                Text = "Resize to",
                Location = new Point(12, 8),
                Size = new Size(356, 124),
            };

            // ── Percent ──────────────────────────────────────────────────────
            _rbPercent.Text = "Percent:";
            _rbPercent.Location = new Point(14, 24);
            _rbPercent.Size = new Size(110, 24);
            _rbPercent.Checked = true;
            _rbPercent.CheckedChanged += (_, __) => UpdateEnabledState();

            _numPercent.Location = new Point(150, 24);
            _numPercent.Size = new Size(80, 23);
            _numPercent.DecimalPlaces = 0;
            _numPercent.Minimum = 1;
            _numPercent.Maximum = 1000;
            _numPercent.Value = 50;

            var lblPct = new Label
            {
                Text = "%",
                Location = new Point(236, 27),
                Size = new Size(24, 20),
            };

            // ── Longest edge ─────────────────────────────────────────────────
            _rbLongest.Text = "Longest edge:";
            _rbLongest.Location = new Point(14, 56);
            _rbLongest.Size = new Size(130, 24);
            _rbLongest.CheckedChanged += (_, __) => UpdateEnabledState();

            _numLongest.Location = new Point(150, 56);
            _numLongest.Size = new Size(80, 23);
            _numLongest.Minimum = 1;
            _numLongest.Maximum = 100000;
            _numLongest.Value = 1024;

            var lblPx = new Label
            {
                Text = "px",
                Location = new Point(236, 59),
                Size = new Size(24, 20),
            };

            // ── Exact width × height ─────────────────────────────────────────
            _rbExact.Text = "Exact size:";
            _rbExact.Location = new Point(14, 88);
            _rbExact.Size = new Size(110, 24);
            _rbExact.CheckedChanged += (_, __) => UpdateEnabledState();

            _numWidth.Location = new Point(150, 88);
            _numWidth.Size = new Size(70, 23);
            _numWidth.Minimum = 1;
            _numWidth.Maximum = 100000;
            _numWidth.Value = 800;

            var lblX = new Label
            {
                Text = "×",
                Location = new Point(224, 91),
                Size = new Size(16, 20),
                TextAlign = ContentAlignment.MiddleCenter,
            };

            _numHeight.Location = new Point(242, 88);
            _numHeight.Size = new Size(70, 23);
            _numHeight.Minimum = 1;
            _numHeight.Maximum = 100000;
            _numHeight.Value = 600;

            var lblPx2 = new Label
            {
                Text = "px",
                Location = new Point(318, 91),
                Size = new Size(24, 20),
            };

            group.Controls.AddRange(new Control[]
            {
                _rbPercent, _numPercent, lblPct,
                _rbLongest, _numLongest, lblPx,
                _rbExact, _numWidth, lblX, _numHeight, lblPx2,
            });

            // ── Allow upscaling ──────────────────────────────────────────────
            _chkUpscale.Text = "Allow enlarging images smaller than the target";
            _chkUpscale.Location = new Point(16, 140);
            _chkUpscale.Size = new Size(352, 24);
            _chkUpscale.Checked = true;

            // ── OK / Cancel ──────────────────────────────────────────────────
            var btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(206, 176),
                Size = new Size(78, 26),
            };
            btnOk.Click += (_, __) => Commit();

            var btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(290, 176),
                Size = new Size(78, 26),
            };

            Controls.Add(group);
            Controls.Add(_chkUpscale);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            UpdateEnabledState();
        }

        /// <summary>Enables only the inputs for the selected mode.</summary>
        private void UpdateEnabledState()
        {
            _numPercent.Enabled = _rbPercent.Checked;
            _numLongest.Enabled = _rbLongest.Checked;
            _numWidth.Enabled = _rbExact.Checked;
            _numHeight.Enabled = _rbExact.Checked;
        }

        /// <summary>Builds <see cref="Spec"/> from the selected mode before closing.</summary>
        private void Commit()
        {
            if (_rbLongest.Checked)
                Spec = SizeSpec.FromLongestEdge((int)_numLongest.Value);
            else if (_rbExact.Checked)
                Spec = SizeSpec.FromExact((int)_numWidth.Value, (int)_numHeight.Value);
            else
                Spec = SizeSpec.FromPercent((double)_numPercent.Value);

            AllowUpscale = _chkUpscale.Checked;
        }
    }
}
