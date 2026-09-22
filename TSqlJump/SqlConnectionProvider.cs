using System;
using System.Data;
using System.Reflection;
using Microsoft.VisualStudio.Shell;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.UI.VSIntegration.ObjectExplorer;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace TSqlJump
{
    public static class SqlConnectionProvider
    {
        private static string GetDatabaseName(string urn)
        {
            if (string.IsNullOrWhiteSpace(urn))
                return null;

            Match match = Regex.Match(
                urn,
                @"Database\[@Name='([^']+)'\]",
                RegexOptions.IgnoreCase);

            return match.Success ? match.Groups[1].Value : null;
        }

        public static IDbConnection GetConnectionFromObjectExplorerSelection(out string DatabaName)
        {
            DatabaName = "";
            ThreadHelper.ThrowIfNotOnUIThread();
            
            try
            {
                var oe = Package.GetGlobalService(typeof(IObjectExplorerService)) as IObjectExplorerService;

                if (oe == null)
                    return null;

                oe.GetSelectedNodes(out int count, out INodeInformation[] selectedNodes);

                if (selectedNodes == null || selectedNodes.Length == 0)
                    return null;

                //selectedNodes[0].Name    -> ağaçta seçili objenin adını verir
                //selectedNodes[0].Context -> Server[@Name='DATASERVER']/Database[@Name='NK_Test']
                //veya detay seçim varsa   -> Server[@Name='DATASERVER']/Database[@Name='NK_Test']/Table[@Name='CompanyDocuments' and @Schema='dbo']
                DatabaName = GetDatabaseName(selectedNodes[0].Context);

                if (string.IsNullOrEmpty(DatabaName))
                    return null;

                //selectedNodes[0].Connection -> dönen örnek veri (database verisi yok!)
                //"Data Source=NARUT;Integrated Security=True;Multiple Active Result Sets=False;Connect Timeout=30;Encrypt=False;Trust Server Certificate=True;Packet Size=4096;Application Name=\"Microsoft SQL Server Management Studio\""
                var connectionInfo = selectedNodes[0].Connection;

                if (connectionInfo == null)
                    return null;

                //connection.ChangeDatabase veya ConnectionString değişimi sorun yaratıyor!
                //bu sebeple aktif database verisi FN ile parametrik olarak geriye gönderiliyor.
                IDbConnection connection = connectionInfo.CreateConnectionObject();
                
                if (connection == null)
                    return null;

                if (connection.State != ConnectionState.Open)
                    connection.Open();

                return connection;
            }
            catch (Exception ex)
            {
                return null;
            }
        }


        public static IDbConnection GetActiveConnection()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                object editorService =
                    GetSqlEditorService();

                if (editorService == null)
                    return null;

                Type interfaceType =
                    FindLoadedType(
                        "Microsoft.SqlServer.Management.UI.VSIntegration.ISqlEditorService");

                if (interfaceType == null)
                    return null;

                object connection =
                    Invoke(
                        editorService,
                        interfaceType,
                        "GetCurrentConnection");

                return connection as IDbConnection;
            }
            catch
            {
                return null;
            }
        }

        private static object GetSqlEditorService()
        {
            Type serviceType =
                FindLoadedType(
                    "Microsoft.SqlServer.Management.UI.VSIntegration.SSqlEditorService");

            if (serviceType == null)
                return null;

            Type interfaceType =
                FindLoadedType(
                    "Microsoft.SqlServer.Management.UI.VSIntegration.ISqlEditorService");

            object service =
                GetGlobalService(serviceType);

            if (service == null && interfaceType != null)
                service = GetGlobalService(interfaceType);

            return service;
        }

        private static object GetGlobalService(Type type)
        {
            try
            {
                return Package.GetGlobalService(type);
            }
            catch
            {
                return null;
            }
        }

        private static object Invoke(
            object instance,
            Type interfaceType,
            string methodName,
            params object[] args)
        {
            MethodInfo method =
                interfaceType.GetMethod(methodName);

            if (method == null)
                return null;

            return method.Invoke(
                instance,
                args);
        }

        private static Type FindLoadedType(
            string fullTypeName)
        {
            foreach (Assembly assembly
                in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type =
                    assembly.GetType(
                        fullTypeName,
                        false);

                if (type != null)
                    return type;
            }

            return null;
        }
    }
}