using EnvDTE;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Sdk.Sfc;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.SqlServer.Management.UI.VSIntegration.ObjectExplorer;

namespace TSqlJump
{

    public static class SqlObjectNavigator
    {

        private sealed class WaitCursorScope : IDisposable
        {
            public WaitCursorScope()
            {
                Cursor.Current = Cursors.WaitCursor;
            }

            public void Dispose()
            {
                Cursor.Current = Cursors.Default;
            }
        }

        public static void Open(string objectType, SqlObjectReference reference, IDbConnection connection)
        {
            if (string.IsNullOrWhiteSpace(objectType))
                throw new ArgumentNullException(nameof(objectType));

            if (reference == null)
                throw new ArgumentNullException(nameof(reference));

            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            switch (objectType)
            {
                case "USER_TABLE":
                    OpenTable(reference, connection);
                    break;

                case "VIEW":
                    OpenView(reference, connection);
                    break;

                case "SQL_STORED_PROCEDURE":
                    OpenStoredProcedure(reference, connection);
                    break;

                case "SQL_SCALAR_FUNCTION":
                case "SQL_TABLE_VALUED_FUNCTION":
                case "SQL_INLINE_TABLE_VALUED_FUNCTION":
                    OpenFunction(reference, connection);
                    break;

                case "SQL_TRIGGER":
                case "CLR_TRIGGER":
                    OpenTrigger(reference, connection);
                    break;

                default:
                    MessageBox.Show(
                        $"Object type '{objectType}' is not supported yet.",
                        "TSqlJump",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    break;
            }
        }







        #region helper_methods
        private static Server GetServer(IDbConnection connection)
        {
            // Eğer connection nesnesi Microsoft.Data.SqlClient.SqlConnection tipindeyse:
            if (connection is Microsoft.Data.SqlClient.SqlConnection sqlConn)
            {
                var serverConn = new ServerConnection(sqlConn);
                return new Server(serverConn);
            }

            // Fallback olarak manuel yapılandırma
            var csb = new DbConnectionStringBuilder();
            csb.ConnectionString = connection.ConnectionString;

            string dataSource = TryGet(csb, "Data Source") ?? TryGet(csb, "Server");
            string userId = TryGet(csb, "User ID") ?? TryGet(csb, "UID");
            string password = TryGet(csb, "Password") ?? TryGet(csb, "PWD");

            string integratedSecurityRaw = TryGet(csb, "Integrated Security") ?? TryGet(csb, "Trusted_Connection");
            bool integratedSecurity = true;

            if (!string.IsNullOrEmpty(integratedSecurityRaw))
            {
                bool.TryParse(integratedSecurityRaw, out integratedSecurity);
            }
            else if (!string.IsNullOrEmpty(userId))
            {
                integratedSecurity = false;
            }

            var serverConnection = new ServerConnection(dataSource)
            {
                LoginSecure = integratedSecurity
            };

            if (!integratedSecurity)
            {
                serverConnection.Login = userId;
                serverConnection.Password = password;
            }

            return new Server(serverConnection);
        }

        private static string TryGet(DbConnectionStringBuilder csb, string key)
        {
            return csb.TryGetValue(key, out var value) ? value?.ToString() : null;
        }

        private static string ResolveDatabase(SqlObjectReference reference, IDbConnection connection)
        {
            return !string.IsNullOrWhiteSpace(reference.Database)
                ? reference.Database
                : connection.Database;
        }

        private static string ResolveSchema(SqlObjectReference reference)
        {
            return string.IsNullOrWhiteSpace(reference.Schema) ? "dbo" : reference.Schema;
        }

        private static void OpenScriptInNewQuery(string script)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            DTE dte = Package.GetGlobalService(typeof(DTE)) as DTE;
            if (dte == null) return;

            dte.ExecuteCommand("File.NewQuery");

            // Yeni query penceresinin aktif IVsTextView'ını al
            var textManager = (IVsTextManager)Package.GetGlobalService(typeof(SVsTextManager));
            textManager.GetActiveView(1, null, out IVsTextView vsView);
            if (vsView == null) return;

            var componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
            var adapterFactory = componentModel.GetService<IVsEditorAdaptersFactoryService>();
            IWpfTextView wpfView = adapterFactory.GetWpfTextView(vsView);
            if (wpfView == null) return;

            using (var edit = wpfView.TextBuffer.CreateEdit())
            {
                edit.Insert(0, script);
                edit.Apply();
            }
        }

        private static string ToAlterScript(ITextObject obj, string schema, string name)
        {
            string header = obj.TextHeader.TrimStart();

            if (header.StartsWith("CREATE", StringComparison.OrdinalIgnoreCase))
                header = "ALTER" + header.Substring("CREATE".Length);

            // TextHeader'daki obje adı sp_rename sonrası ESKİ isimde kalmış olabilir
            // (SQL Server, sys.objects.name'i günceller ama saklı script metnini güncellemez).
            // Bu yüzden CREATE/ALTER'dan hemen sonraki ilk "schema.isim" kalıbını
            // SMO'nun GÜNCEL Schema/Name bilgisiyle değiştiriyoruz.
            var qualifiedNamePattern = new Regex(@"(\[[^\]]+\]|\w+)\s*\.\s*(\[[^\]]+\]|\w+)");
            header = qualifiedNamePattern.Replace(header, $"[{schema}].[{name}]", 1);

            return header + obj.TextBody;
        }

#endregion

        // ---------- TABLO ----------

        private static void OpenTable(SqlObjectReference reference, IDbConnection connection)
        {
            //TEST
            //MessageBox.Show(
            //    string.Format(
            //        "TABLE\n\nServer: {0}\nDatabase: {1}\nSchema: {2}\nObject: {3}\n\nConnection: {4}",
            //        reference.Server ?? "(current)",
            //        reference.Database ?? "(current)",
            //        reference.Schema ?? "(default)",
            //        reference.ObjectName,
            //        connection.GetType().FullName),
            //    "TSqlJump - Open Table",
            //    MessageBoxButtons.OK,
            //    MessageBoxIcon.Information);


            try
            {
                using (new WaitCursorScope())
                {
                    var server = GetServer(connection);
                    var db = server.Databases[ResolveDatabase(reference, connection)];
                    var table = db.Tables[reference.ObjectName, ResolveSchema(reference)];

                    if (table == null)
                    {
                        MessageBox.Show($"Table '{reference.ObjectName}' not found.", "TSqlJump",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    /*
                            Options = {
                                ScriptDrops = false,
                                WithDependencies = false,
                                DriAll = true,
                                ExtendedProperties = true,
                                Indexes = true,
                                Triggers = true,
                                ClusteredIndexes = true,
                                NonClusteredIndexes = true
                            }                     
                     */

                    var scripter = new Scripter(server)
                    {
                        Options =
                            {
                                ScriptDrops = false,
                                WithDependencies = false,
                                DriAll = true,
                                ExtendedProperties = true //MS_Description vs..
                            }
                    };

                    var scriptLines = scripter.Script(new Urn[] { table.Urn });
                    string fullScript = string.Join(Environment.NewLine, scriptLines.Cast<string>());

                    OpenScriptInNewQuery(fullScript);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "TSqlJump - Open Table Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------- STORED PROCEDURE ----------

        private static void OpenStoredProcedure(SqlObjectReference reference, IDbConnection connection)
        {
            try
            {
                using (new WaitCursorScope())
                {
                    var server = GetServer(connection);
                    var db = server.Databases[ResolveDatabase(reference, connection)];
                    var sp = db.StoredProcedures[reference.ObjectName, ResolveSchema(reference)];

                    if (sp == null)
                    {
                        MessageBox.Show($"Stored procedure '{reference.ObjectName}' not found.", "TSqlJump",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    OpenScriptInNewQuery(ToAlterScript(sp, sp.Schema, sp.Name));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "TSqlJump - Open SP Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------- VIEW ----------

        private static void OpenView(SqlObjectReference reference, IDbConnection connection)
        {
            try
            {
                using (new WaitCursorScope())
                {
                    var server = GetServer(connection);
                    var db = server.Databases[ResolveDatabase(reference, connection)];
                    var view = db.Views[reference.ObjectName, ResolveSchema(reference)];

                    if (view == null)
                    {
                        MessageBox.Show($"View '{reference.ObjectName}' not found.", "TSqlJump",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    OpenScriptInNewQuery(ToAlterScript(view, view.Schema, view.Name));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "TSqlJump - Open View Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------- FUNCTION ----------

        private static void OpenFunction(SqlObjectReference reference, IDbConnection connection)
        {
            try
            {
                using (new WaitCursorScope())
                {
                    var server = GetServer(connection);
                    var db = server.Databases[ResolveDatabase(reference, connection)];
                    var func = db.UserDefinedFunctions[reference.ObjectName, ResolveSchema(reference)];

                    if (func == null)
                    {
                        MessageBox.Show($"Function '{reference.ObjectName}' not found.", "TSqlJump",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    OpenScriptInNewQuery(ToAlterScript(func, func.Schema, func.Name));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "TSqlJump - Open Function Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------- TRIGGER ----------

        private static void OpenTrigger(SqlObjectReference reference, IDbConnection connection)
        {
            try
            {
                using (new WaitCursorScope())
                {
                    var server = GetServer(connection);
                    var db = server.Databases[ResolveDatabase(reference, connection)];

                    // Trigger'lar tabloya bağlı; hangi tabloya ait olduğunu bilmiyorsak
                    // tüm tablolarda arıyoruz (DDL/server-level trigger'lar bu kapsamda değil).
                    Trigger trigger = db.Tables
                        .Cast<Table>()
                        .SelectMany(t => t.Triggers.Cast<Trigger>())
                        .FirstOrDefault(t => t.Name.Equals(reference.ObjectName, StringComparison.OrdinalIgnoreCase));

                    if (trigger == null)
                    {
                        MessageBox.Show($"Trigger '{reference.ObjectName}' not found.", "TSqlJump",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    var parentTable = (Table)trigger.Parent;
                    OpenScriptInNewQuery(ToAlterScript(trigger, parentTable.Schema, trigger.Name));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "TSqlJump - Open Trigger Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        //Locate in Object Explorer
        public static void Locate(string objectType, SqlObjectReference reference, IDbConnection connection)
        {
            try
            {
                using (new WaitCursorScope())
                {
                    var server = GetServer(connection);
                    var db = server.Databases[ResolveDatabase(reference, connection)];
                    string schema = ResolveSchema(reference);

                    Urn urn = null;

                    switch (objectType)
                    {
                        case "USER_TABLE":
                            urn = db.Tables[reference.ObjectName, schema]?.Urn;
                            break;

                        case "VIEW":
                            urn = db.Views[reference.ObjectName, schema]?.Urn;
                            break;

                        case "SQL_STORED_PROCEDURE":
                            urn = db.StoredProcedures[reference.ObjectName, schema]?.Urn;
                            break;

                        case "SQL_SCALAR_FUNCTION":
                        case "SQL_TABLE_VALUED_FUNCTION":
                        case "SQL_INLINE_TABLE_VALUED_FUNCTION":
                            urn = db.UserDefinedFunctions[reference.ObjectName, schema]?.Urn;
                            break;

                        case "SQL_TRIGGER":
                        case "CLR_TRIGGER":
                            urn = db.Tables.Cast<Table>()
                                    .SelectMany(t => t.Triggers.Cast<Trigger>())
                                    .FirstOrDefault(t => t.Name.Equals(reference.ObjectName, StringComparison.OrdinalIgnoreCase))
                                    ?.Urn;
                            break;
                    }

                    if (urn == null)
                    {
                        MessageBox.Show($"Object '{reference.ObjectName}' not found.", "TSqlJump",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    var oe = Package.GetGlobalService(typeof(IObjectExplorerService)) as IObjectExplorerService;
                    if (oe == null)
                    {
                        MessageBox.Show("IObjectExplorerService could not be accessed.", "TSqlJump",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    INodeInformation node = oe.FindNode(urn.ToString());

                    if (node == null)
                    {
                        MessageBox.Show(
                            "Node not found in Object Explorer.\nIs this server connected and expanded at least once in Object Explorer?",
                            "TSqlJump", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    //IObjectExplorerService -> SqlWorkbench.Interfaces.dll
                    //Resolved via reverse engineering...
                    //SqlWorkbench.Interfaces\Microsoft\SqlServer\Management\UI\VSIntegration\ObjectExplorer
                    oe.SynchronizeTree(node);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "TSqlJump - Locate Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }



    }
}