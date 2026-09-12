using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace TSqlJump
{
    public sealed class CodeTemplateForm : Form
    {
        private readonly CodeTemplateRepository repository;

        private TextBox txtSearch;
        private ListView lstSnippets;

        private List<CodeTemplateSnippet> snippets;

        /// <summary>
        /// Kullanıcının seçip kabul ettiği snippet.
        /// </summary>
        public CodeTemplateSnippet SelectedSnippet { get; private set; }


        public CodeTemplateForm()
        {
            CodeTemplateInitializer.EnsureDefaultFiles();

            repository = new CodeTemplateRepository(
                CodeTemplatePaths.SnippetsFile);

            InitializeForm();
            LoadSnippets();
        }

        private void InitializeForm()
        {
            Text = "CodeTemplates";

            StartPosition =
                FormStartPosition.CenterScreen;

            Width = 680;
            Height = 600;

            MinimizeBox = false;
            MaximizeBox = false;

            MinimumSize =
                new Size(600, 400);

            KeyPreview = true;



            var statusStrip = new StatusStrip
            {
                Dock = DockStyle.Bottom
            };

            var lblFile =
                new ToolStripStatusLabel
                {
                    Text = "Templates.toml",
                    IsLink = true,
                    ToolTipText = "Open the definition file."
                };

            lblFile.Click +=
                (sender, e) =>
                {
                    OpenSnippetFile();
                };



            txtSearch = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 38,
                Font = new Font(
                    "Segoe UI",
                    13F)
            };

            txtSearch.TextChanged +=
                TxtSearch_TextChanged;

            txtSearch.KeyDown +=
                TxtSearch_KeyDown;

            this.KeyDown += Form_KeyDown;

            lstSnippets = new ListView
            {
                Dock = DockStyle.Fill,

                View = View.Details,

                FullRowSelect = true,
                GridLines = false,
                HideSelection = false,

                Font = new Font(
                    "Segoe UI",
                    12F),

                OwnerDraw = true
            };

            lstSnippets.Columns.Add(
                "Short Name",
                120);

            lstSnippets.Columns.Add(
                "SQL Statement",
                430);

            lstSnippets.Columns.Add(
                "Shortcut",
                100);

            var imageList = new ImageList
            {
                ImageSize = new Size(1, 34)
            };

            lstSnippets.SmallImageList =
                imageList;

            lstSnippets.DrawColumnHeader +=
                LstSnippets_DrawColumnHeader;

            lstSnippets.DrawItem +=
                LstSnippets_DrawItem;

            lstSnippets.DrawSubItem +=
                LstSnippets_DrawSubItem;

            lstSnippets.DoubleClick +=
                LstSnippets_DoubleClick;

            lstSnippets.KeyDown +=
                LstSnippets_KeyDown;


            statusStrip.Items.Add(lblFile);
            Controls.Add(statusStrip);
            Controls.Add(lstSnippets);
            Controls.Add(txtSearch);

            Shown +=
                CodeTemplateForm_Shown;
        }

        private void CodeTemplateForm_Shown(
            object sender,
            EventArgs e)
        {
            txtSearch.Focus();
        }

        private void LoadSnippets()
        {
            snippets = repository.GetAll();

            RefreshList(snippets);
        }

        private void RefreshList(
            IEnumerable<CodeTemplateSnippet> items)
        {
            lstSnippets.BeginUpdate();

            try
            {
                lstSnippets.Items.Clear();

                foreach (CodeTemplateSnippet snippet in items)
                {
                    var item =
                        new ListViewItem(
                            snippet.ShortName);

                    item.SubItems.Add(
                        snippet.Name);

                    item.SubItems.Add(
                        snippet.Shortcut ?? "");

                    item.Tag = snippet;

                    lstSnippets.Items.Add(item);
                }
            }
            finally
            {
                lstSnippets.EndUpdate();
            }
        }

        private void TxtSearch_TextChanged(
            object sender,
            EventArgs e)
        {
            string search =
                txtSearch.Text.Trim();

            if (search.Length == 0)
            {
                RefreshList(snippets);
                return;
            }

            var filtered =
                new List<CodeTemplateSnippet>();

            foreach (CodeTemplateSnippet snippet in snippets)
            {
                if (snippet.ShortName.IndexOf(
                        search,
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    snippet.Name.IndexOf(
                        search,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    filtered.Add(snippet);
                }
            }

            RefreshList(filtered);
        }

        private void Form_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode >= Keys.F1 &&
                e.KeyCode <= Keys.F12)
            {
                string shortcut = e.KeyCode.ToString();

                CodeTemplateSnippet snippet = null;

                foreach (CodeTemplateSnippet item in snippets)
                {
                    if (string.Equals(
                        item.Shortcut,
                        shortcut,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        snippet = item;
                        break;
                    }
                }

                if (snippet == null)
                    return;

                AcceptSnippet(snippet);

                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }


        /// <summary>
        /// Arama kutusundayken ENTER:
        /// listenin ilk kaydını kabul eder.
        /// ESC:
        /// arama doluysa temizler,
        /// boşsa formu kapatır.
        /// </summary>
        private void TxtSearch_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                AcceptFirstSnippet();

                e.Handled = true;
                e.SuppressKeyPress = true;

                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                HandleEscape();

                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        /// <summary>
        /// Liste odaktayken ENTER.
        /// </summary>
        private void LstSnippets_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                AcceptSelectedSnippet();

                e.Handled = true;
                e.SuppressKeyPress = true;

                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                HandleEscape();

                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void HandleEscape()
        {
            if (txtSearch.Text.Length > 0)
            {
                txtSearch.Clear();
                txtSearch.Focus();
                return;
            }

            DialogResult =
                DialogResult.Cancel;

            Close();
        }

        /// <summary>
        /// Arama kutusundan ENTER geldiğinde
        /// filtrelenmiş listenin ilk kaydını kabul eder.
        /// </summary>
        private void AcceptFirstSnippet()
        {
            if (lstSnippets.Items.Count == 0)
                return;

            ListViewItem item =
                lstSnippets.Items[0];

            CodeTemplateSnippet snippet =
                item.Tag as CodeTemplateSnippet;

            if (snippet == null)
                return;

            AcceptSnippet(snippet);
        }

        /// <summary>
        /// Liste üzerinden ENTER veya çift tıklama.
        /// </summary>
        private void AcceptSelectedSnippet()
        {
            if (lstSnippets.SelectedItems.Count == 0)
                return;

            CodeTemplateSnippet snippet =
                lstSnippets.SelectedItems[0]
                    .Tag as CodeTemplateSnippet;

            if (snippet == null)
                return;

            AcceptSnippet(snippet);
        }

        private void AcceptSnippet(
            CodeTemplateSnippet snippet)
        {
            SelectedSnippet = snippet;

            DialogResult =
                DialogResult.OK;

            Close();
        }

        private void LstSnippets_DoubleClick(
            object sender,
            EventArgs e)
        {
            AcceptSelectedSnippet();
        }

        private void LstSnippets_DrawColumnHeader(
            object sender,
            DrawListViewColumnHeaderEventArgs e)
        {
            using (var brush =
                new SolidBrush(
                    SystemColors.Control))
            {
                e.Graphics.FillRectangle(
                    brush,
                    e.Bounds);
            }

            using (var font =
                new Font(
                    "Segoe UI",
                    11F,
                    FontStyle.Bold))
            {
                TextRenderer.DrawText(
                    e.Graphics,
                    e.Header.Text,
                    font,
                    e.Bounds,
                    SystemColors.ControlText,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter);
            }
        }

        private void LstSnippets_DrawItem(
            object sender,
            DrawListViewItemEventArgs e)
        {
            // Asıl satır çizimi DrawSubItem'da yapılıyor.
        }

        private void LstSnippets_DrawSubItem(
            object sender,
            DrawListViewSubItemEventArgs e)
        {
            bool selected =
                e.Item.Selected;

            bool alternateRow =
                e.ItemIndex % 2 == 1;

            Color backColor;

            if (selected)
            {
                backColor =
                    SystemColors.Highlight;
            }
            else if (alternateRow)
            {
                backColor =
                    Color.FromArgb(
                        245,
                        245,
                        245);
            }
            else
            {
                backColor =
                    SystemColors.Window;
            }

            using (var brush =
                new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(
                    brush,
                    e.Bounds);
            }

            Color textColor =
                selected
                    ? SystemColors.HighlightText
                    : SystemColors.WindowText;

            TextRenderer.DrawText(
                e.Graphics,
                e.SubItem.Text,
                lstSnippets.Font,
                new Rectangle(
                    e.Bounds.X + 8,
                    e.Bounds.Y,
                    e.Bounds.Width - 16,
                    e.Bounds.Height),
                textColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
        }


        private void OpenSnippetFile()
        {
            try
            {
                if (!File.Exists(CodeTemplatePaths.SnippetsFile))
                {
                    MessageBox.Show(
                        "Definition file not found:\r\n\r\n" +
                        CodeTemplatePaths.SnippetsFile,
                        "TSqlJump",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName =
                            CodeTemplatePaths.SnippetsFile,

                        UseShellExecute = true
                    });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Definition file could not be opened.\r\n\r\n" +
                    ex.Message,
                    "TSqlJump",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OpenSnippetFolder()
        {
            try
            {
                CodeTemplatePaths.EnsureDirectory();

                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName =
                            CodeTemplatePaths.RootDirectory,

                        UseShellExecute = true
                    });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The TSqlJump folder could not be opened.\r\n\r\n" +
                    ex.Message,
                    "Definition file could not be opened.",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

    }
}