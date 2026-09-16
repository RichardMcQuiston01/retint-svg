using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ImageTools.Core;

namespace SVGToolsShell
{
    /// <summary>
    /// Edits the common EXIF and IPTC metadata of a single image. Values are
    /// prefilled from the file (read by <see cref="ExifTool"/> before the dialog
    /// opens) and written back on Save via ExifTool, which keeps a
    /// <c>&lt;name&gt;_original</c> backup — nothing is destroyed. An empty field
    /// clears that tag.
    /// </summary>
    internal sealed class MetadataDialog : Form
    {
        private readonly string _filePath;

        private readonly TextBox _artist = new TextBox();
        private readonly TextBox _copyright = new TextBox();
        private readonly TextBox _description = new TextBox();
        private readonly TextBox _dateTaken = new TextBox();
        private readonly TextBox _title = new TextBox();
        private readonly TextBox _caption = new TextBox();
        private readonly TextBox _keywords = new TextBox();
        private readonly TextBox _creator = new TextBox();
        private readonly TextBox _city = new TextBox();
        private readonly TextBox _country = new TextBox();
        private readonly Label _status = new Label();
        private readonly Button _save = new Button();

        public MetadataDialog(string filePath, ImageMetadata current)
        {
            _filePath = filePath;

            Text = "Edit Metadata — " + Path.GetFileName(filePath);
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(520, 588);
            MinimumSize = new Size(480, 560);
            Font = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;

            var y = 12;
            AddSection("EXIF", ref y);
            AddRow("Artist:", _artist, ref y);
            AddRow("Copyright:", _copyright, ref y);
            AddRow("Description:", _description, ref y, multiline: true);
            AddRow("Date taken:", _dateTaken, ref y, hint: "YYYY:MM:DD HH:MM:SS");

            y += 6;
            AddSection("IPTC", ref y);
            AddRow("Title:", _title, ref y);
            AddRow("Caption:", _caption, ref y, multiline: true);
            AddRow("Keywords:", _keywords, ref y, hint: "separate with commas");
            AddRow("Creator:", _creator, ref y);
            AddRow("City:", _city, ref y);
            AddRow("Country:", _country, ref y);

            // Prefill from the file's current values.
            _artist.Text = current?.Artist ?? "";
            _copyright.Text = current?.Copyright ?? "";
            _description.Text = current?.Description ?? "";
            _dateTaken.Text = current?.DateTaken ?? "";
            _title.Text = current?.Title ?? "";
            _caption.Text = current?.Caption ?? "";
            _keywords.Text = current?.Keywords ?? "";
            _creator.Text = current?.Creator ?? "";
            _city.Text = current?.City ?? "";
            _country.Text = current?.Country ?? "";

            _status.Location = new Point(12, ClientSize.Height - 66);
            _status.Size = new Size(ClientSize.Width - 24, 24);
            _status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _status.ForeColor = SystemColors.GrayText;
            _status.Text = "Saving keeps the original as \"" + Path.GetFileName(filePath) + "_original\".";

            _save.Text = "Save";
            _save.Size = new Size(84, 28);
            _save.Location = new Point(ClientSize.Width - 176, ClientSize.Height - 38);
            _save.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _save.Click += (_, __) => OnSave();

            var cancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Size = new Size(84, 28),
                Location = new Point(ClientSize.Width - 88, ClientSize.Height - 38),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            };

            Controls.Add(_status);
            Controls.Add(_save);
            Controls.Add(cancel);
            AcceptButton = _save;
            CancelButton = cancel;
        }

        /// <summary>Adds a bold section header at the running y position.</summary>
        private void AddSection(string title, ref int y)
        {
            var lbl = new Label
            {
                Text = title,
                Location = new Point(12, y),
                Size = new Size(ClientSize.Width - 24, 20),
                Font = new Font(Font, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };
            Controls.Add(lbl);
            y += 24;
        }

        /// <summary>Adds a labeled text box (optionally multiline / with a grey hint)
        /// at the running y position and advances it.</summary>
        private void AddRow(string label, TextBox box, ref int y, bool multiline = false, string? hint = null)
        {
            var lbl = new Label
            {
                Text = label,
                Location = new Point(24, y + 3),
                Size = new Size(92, 20),
            };
            box.Location = new Point(120, y);
            box.Size = new Size(ClientSize.Width - 120 - 16, multiline ? 44 : 23);
            box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            if (multiline)
            {
                box.Multiline = true;
                box.ScrollBars = ScrollBars.Vertical;
            }

            Controls.Add(lbl);
            Controls.Add(box);
            y += (multiline ? 44 : 23) + 8;

            if (!string.IsNullOrEmpty(hint))
            {
                var h = new Label
                {
                    Text = hint,
                    Location = new Point(120, y - 4),
                    Size = new Size(ClientSize.Width - 120 - 16, 16),
                    ForeColor = SystemColors.GrayText,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                };
                Controls.Add(h);
                y += 16;
            }
        }

        private ImageMetadata Collect() => new ImageMetadata
        {
            Artist = _artist.Text,
            Copyright = _copyright.Text,
            Description = _description.Text,
            DateTaken = _dateTaken.Text,
            Title = _title.Text,
            Caption = _caption.Text,
            Keywords = _keywords.Text,
            Creator = _creator.Text,
            City = _city.Text,
            Country = _country.Text,
        };

        private void OnSave()
        {
            _save.Enabled = false;
            var previousCursor = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            ExifTool.WriteResult result;
            try
            {
                result = ExifTool.Write(_filePath, Collect());
            }
            finally
            {
                Cursor.Current = previousCursor;
                _save.Enabled = true;
            }

            if (result.Success)
            {
                MessageBox.Show(result.Message, "Edit Metadata",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                MessageBox.Show(result.Message, "Edit Metadata",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
