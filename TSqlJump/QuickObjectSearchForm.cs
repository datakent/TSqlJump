using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TSqlJump
{
    public enum QuickObjectSearchAction
    {
        GoToObject,
        Locate
    }

    public sealed class QuickObjectSearchForm : Form
    {
        private readonly IDbConnection templateConnection;
        private readonly string database;

        private TextBox txtSearch;
        private ListView lstResults;
        private Button btnGoToObject;
        private Button btnLocate;
        private Button btnCancel;

        private readonly Timer debounceTimer;
        private int searchGeneration;

        public SqlObjectReference SelectedReference { get; private set; }
        public string SelectedObjectType { get; private set; }
        public QuickObjectSearchAction SelectedAction { get; private set; }

        public QuickObjectSearchForm(IDbConnection connection)
        {
            templateConnection = connection;
            database = connection.Database;

            debounceTimer = new Timer { Interval = 300 };
            debounceTimer.Tick += (s, e) => { debounceTimer.Stop(); PerformSearch(); };

            InitializeForm();
        }

        private void InitializeForm()
        {
            Text = "Quick Object Search";
            StartPosition = FormStartPosition.CenterScreen;
            Width = 680;
            Height = 560;
            MinimizeBox = false;
            MaximizeBox = false;
            MinimumSize = new Size(560, 400);
            KeyPreview = true;
            ShowIcon = false;

            txtSearch = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 38,
                Font = new Font("Segoe UI", 13F)
            };
            txtSearch.TextChanged += TxtSearch_TextChanged;
            txtSearch.KeyDown += TxtSearch_KeyDown;

            lstResults = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                Font = new Font("Segoe UI", 12F)
            };
            lstResults.Columns.Add("Object Name", 400);
            lstResults.Columns.Add("Object Type", 180);
            lstResults.DoubleClick += (s, e) => TryAccept(QuickObjectSearchAction.GoToObject);
            lstResults.KeyDown += LstResults_KeyDown;

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8),
                Font = new Font("Segoe UI", 13F)
            };

            btnGoToObject = new Button { Text = "Go to Object (F8)", Width = 200, Height = 40, Top = 1 };
            btnGoToObject.Click += (s, e) => TryAccept(QuickObjectSearchAction.GoToObject);

            btnLocate = new Button { Text = "Locate in Object Explorer (F9)", Width = 320, Height = 40, Top = 1 };
            btnLocate.Click += (s, e) => TryAccept(QuickObjectSearchAction.Locate);

            btnCancel = new Button { Text = "Cancel", Width = 90, Height = 40, Top = 1 };
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            buttonPanel.Controls.Add(btnCancel);
            buttonPanel.Controls.Add(btnLocate);
            buttonPanel.Controls.Add(btnGoToObject);

            Controls.Add(lstResults);
            Controls.Add(buttonPanel);
            Controls.Add(txtSearch);

            KeyDown += QuickObjectSearchForm_KeyDown;
            Shown += (s, e) => txtSearch.Focus();
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            debounceTimer.Stop();

            if (txtSearch.Text.Trim().Length == 0)
            {
                lstResults.Items.Clear();
                return;
            }

            debounceTimer.Start();
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                HandleEscape();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.Return)
            {
                TryAccept(QuickObjectSearchAction.GoToObject);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Return)
            {
                TryAccept(QuickObjectSearchAction.Locate);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }

            if (e.KeyCode == Keys.Down)
            {
                MoveSelection(1);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Up)
            {
                MoveSelection(-1);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
        }

        private void MoveSelection(int direction)
        {
            if (lstResults.Items.Count == 0)
                return;

            int currentIndex = lstResults.SelectedIndices.Count > 0
                ? lstResults.SelectedIndices[0]
                : -1;

            int newIndex = currentIndex + direction;

            if (newIndex < 0)
                newIndex = 0;

            if (newIndex >= lstResults.Items.Count)
                newIndex = lstResults.Items.Count - 1;

            foreach (ListViewItem item in lstResults.SelectedItems)
                item.Selected = false;

            lstResults.Items[newIndex].Selected = true;
            lstResults.Items[newIndex].EnsureVisible();
        }

        private void QuickObjectSearchForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F8)
            {
                TryAccept(QuickObjectSearchAction.GoToObject);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.F9)
            {
                TryAccept(QuickObjectSearchAction.Locate);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void LstResults_KeyDown(object sender, KeyEventArgs e)
        {
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

            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void PerformSearch()
        {
            string pattern = txtSearch.Text.Trim();

            if (pattern.Length == 0)
            {
                lstResults.Items.Clear();
                return;
            }

            int generation = ++searchGeneration;

            Task.Run(() => QueryObjects(templateConnection, database, pattern))
                .ContinueWith(t =>
                {
                    if (generation != searchGeneration)
                        return;

                    if (t.IsFaulted || IsDisposed)
                        return;

                    BeginInvoke(new Action(() => PopulateResults(t.Result)));
                });
        }

        private void PopulateResults(List<QuickSearchResult> results)
        {
            lstResults.BeginUpdate();
            try
            {
                lstResults.Items.Clear();

                foreach (var r in results)
                {
                    var item = new ListViewItem($"{r.Schema}.{r.ObjectName}");
                    item.SubItems.Add(DisplayType(r.TypeCode));
                    item.Tag = r;
                    lstResults.Items.Add(item);
                }

                if (lstResults.Items.Count > 0)
                    lstResults.Items[0].Selected = true;
            }
            finally
            {
                lstResults.EndUpdate();
            }
        }

        private void TryAccept(QuickObjectSearchAction action)
        {
            if (lstResults.SelectedItems.Count == 0)
                return;

            if (!(lstResults.SelectedItems[0].Tag is QuickSearchResult match))
                return;

            string mappedType = MapObjectType(match.TypeCode);

            if (mappedType == null)
                return;

            SelectedReference = new SqlObjectReference
            {
                Database = database,
                Schema = match.Schema,
                ObjectName = match.ObjectName
            };

            SelectedObjectType = mappedType;
            SelectedAction = action;

            DialogResult = DialogResult.OK;
            Close();
        }

        private static List<QuickSearchResult> QueryObjects(IDbConnection template, string database, string pattern)
        {
            var results = new List<QuickSearchResult>();

            if (!(template is ICloneable cloneable))
                return results;

            using (var connection = (IDbConnection)cloneable.Clone())
            {
                connection.Open();

                if (!string.IsNullOrEmpty(database))
                    connection.ChangeDatabase(database);

                using (IDbCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT TOP (100) s.name, o.name, o.type
                        FROM sys.objects o
                        JOIN sys.schemas s ON s.schema_id = o.schema_id
                        WHERE o.type IN ('U','V','P','FN','TF','IF','TR')
                          AND o.name LIKE @pattern
                        ORDER BY o.name";

                    IDbDataParameter param = cmd.CreateParameter();
                    param.ParameterName = "@pattern";
                    param.Value = "%" + EscapeLike(pattern) + "%";
                    cmd.Parameters.Add(param);

                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new QuickSearchResult
                            {
                                Schema = reader.GetString(0),
                                ObjectName = reader.GetString(1),
                                TypeCode = reader.GetString(2).Trim()
                            });
                        }
                    }
                }
            }

            return results;
        }

        private static string EscapeLike(string value)
        {
            return value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
        }

        private static string MapObjectType(string typeCode)
        {
            switch (typeCode)
            {
                case "U": return "USER_TABLE";
                case "V": return "VIEW";
                case "P": return "SQL_STORED_PROCEDURE";
                case "FN": return "SQL_SCALAR_FUNCTION";
                case "TF": return "SQL_TABLE_VALUED_FUNCTION";
                case "IF": return "SQL_INLINE_TABLE_VALUED_FUNCTION";
                case "TR": return "SQL_TRIGGER";
                case "TA": return "CLR_TRIGGER";
                default: return null;
            }
        }

        private static string DisplayType(string typeCode)
        {
            switch (typeCode)
            {
                case "U": return "Table";
                case "V": return "View";
                case "P": return "Stored Procedure";
                case "FN":
                case "TF":
                case "IF": return "Function";
                case "TR":
                case "TA": return "Trigger";
                default: return typeCode;
            }
        }
    }

    internal sealed class QuickSearchResult
    {
        public string Schema { get; set; }
        public string ObjectName { get; set; }
        public string TypeCode { get; set; }
    }
}