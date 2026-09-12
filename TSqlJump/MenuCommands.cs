using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.Data.SqlClient;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.SqlServer.Management.UI.VSIntegration.ObjectExplorer;

namespace TSqlJump
{
    internal sealed class MenuCommands
    {
        public const int CodeTemplateCommandId = 0x0100;
        public const int GoToObjectCommandId = 0x0101;
        public const int LocateObjectCommandId = 0x0102;

        public static readonly Guid CommandSet = new Guid("d6c3c7e1-4c76-4c4d-9f3e-7e2e5c6a8b11");


        private readonly AsyncPackage package;

        private MenuCommands(
            AsyncPackage package,
            OleMenuCommandService commandService)
        {
            this.package = package;

            var commandId = new CommandID(CommandSet, CodeTemplateCommandId);
            var menuCommand = new MenuCommand(ShowCodeTemplateForm,commandId);

            var goToObjectCommandId = new CommandID(CommandSet, GoToObjectCommandId);
            var goToObjectCommand = new MenuCommand(GoToObject, goToObjectCommandId);

            var locateCommandId = new CommandID(CommandSet, LocateObjectCommandId);
            var locateCommand = new MenuCommand(LocateInObjectExplorer, locateCommandId);
            
            commandService.AddCommand(locateCommand);
            commandService.AddCommand(goToObjectCommand);
            commandService.AddCommand(menuCommand);
        }

        public static async System.Threading.Tasks.Task InitializeAsync(
            AsyncPackage package)
        {
            await package.JoinableTaskFactory
                .SwitchToMainThreadAsync();

            var commandService =
                await package.GetServiceAsync(
                    typeof(IMenuCommandService))
                as OleMenuCommandService;

            if (commandService == null)
                return;

            _ = new MenuCommands(package, commandService);
        }



        private bool TryResolveCursorObject(
            out string objectType,
            out SqlObjectReference reference,
            out System.Data.IDbConnection connection,
            out string failureMessage,
            out MessageBoxIcon failureIcon)
        {
            objectType = null;
            reference = null;
            connection = null;
            failureMessage = null;
            failureIcon = MessageBoxIcon.Warning;

            ThreadHelper.ThrowIfNotOnUIThread();

            DTE dte = Package.GetGlobalService(typeof(DTE)) as DTE;

            if (dte == null)
            {
                failureMessage = "The SSMS DTE service was inaccessible.";
                failureIcon = MessageBoxIcon.Error;
                return false;
            }

            Document activeDocument = dte.ActiveDocument;

            if (activeDocument == null)
            {
                failureMessage = "No active SQL editor was found.";
                return false;
            }

            TextSelection selection = activeDocument.Selection as TextSelection;

            if (selection == null)
            {
                failureMessage = "The active editor's text selection was inaccessible.";
                return false;
            }

            TextPoint activePoint = selection.ActivePoint;
            TextDocument textDocument = activeDocument.Object("TextDocument") as TextDocument;

            if (textDocument == null)
            {
                failureMessage = "The active document is not a text document.";
                return false;
            }

            EditPoint linePoint = textDocument.StartPoint.CreateEditPoint();
            linePoint.MoveToLineAndOffset(activePoint.Line, 1);
            string lineText = linePoint.GetText(linePoint.LineLength);

            reference = SqlObjectParser.Parse(lineText, activePoint.LineCharOffset);

            if (reference == null)
            {
                failureMessage = "No SQL object was found at the cursor.";
                failureIcon = MessageBoxIcon.Information;
                return false;
            }

            connection = SqlConnectionProvider.GetActiveConnection();

            if (connection == null)
            {
                failureMessage = "No active SQL Server connection was found.";
                return false;
            }

            objectType = SqlObjectResolver.GetObjectType(connection, reference);

            if (objectType == null)
            {
                failureMessage = $"Object type for '{reference.ObjectName}' could not be resolved.";
                failureIcon = MessageBoxIcon.Information;
                return false;
            }

            return true;
        }



        private void LocateInObjectExplorer(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!TryResolveCursorObject(out string objectType, out SqlObjectReference reference,
                out System.Data.IDbConnection connection, out string failureMessage, out MessageBoxIcon failureIcon))
            {
                MessageBox.Show(failureMessage, "TSqlJump", MessageBoxButtons.OK, failureIcon);
                return;
            }

            SqlObjectNavigator.Locate(objectType, reference, connection);
        }


        private void GoToObject(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!TryResolveCursorObject(out string objectType, out SqlObjectReference reference,
                out System.Data.IDbConnection connection, out string failureMessage, out MessageBoxIcon failureIcon))
            {
                MessageBox.Show(failureMessage, "TSqlJump", MessageBoxButtons.OK, failureIcon);
                return;
            }

            SqlObjectNavigator.Open(objectType, reference, connection);

            //** TEST **
            //MessageBox.Show(
            //    string.Format(
            //        "Object:\r\n{0}\r\n\r\nType:\r\n{1}",
            //        reference.ObjectName,
            //        objectType ?? "(not found)"),
            //    "TSqlJump - Object Type",
            //    MessageBoxButtons.OK,
            //    MessageBoxIcon.Information);



            //** TEST **
            //MessageBox.Show(
            //    string.Format(
            //        "Data Source: {0}\r\nDatabase: {1}\r\n\r\nConnection String:\r\n{2}\r\n\r\n{3}",
            //        connection.GetType().GetProperty("DataSource")?.GetValue(connection),
            //        connection.GetType().GetProperty("Database")?.GetValue(connection),
            //        connection.ConnectionString, reference.ToString()),
            //    "TSqlJump - Active Connection",
            //    MessageBoxButtons.OK,
            //    MessageBoxIcon.Information);
        }




        private static object GetProperty(object instance, string propertyName)
        {
            if (instance == null)
                return null;

            PropertyInfo property =
                instance.GetType().GetProperty(propertyName);

            return property?.GetValue(instance);
        }

        private void ShowCodeTemplateForm(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            DTE dte =
                Package.GetGlobalService(
                    typeof(DTE)) as DTE;

            if (dte == null)
            {
                MessageBox.Show(
                    "The SSMS DTE service was inaccessible.",
                    "TSqlJump",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return;
            }

            Document activeDocument =
                dte.ActiveDocument;

            if (activeDocument == null)
            {
                MessageBox.Show(
                    "No active SQL editor was found.",
                    "TSqlJump",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            TextSelection selection = activeDocument.Selection as TextSelection;

            if (selection == null)
            {
                MessageBox.Show(
                    "The active editor's text selection was inaccessible.",
                    "TSqlJump",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            using (var form =
                new CodeTemplateForm())
            {
                DialogResult result =
                    form.ShowDialog();

                if (result != DialogResult.OK)
                    return;

                CodeTemplateSnippet snippet =
                    form.SelectedSnippet;

                if (snippet == null)
                    return;

                selection.Insert(
                    snippet.TSql);
            }
        }

    }
}