using System;
using System.Data;
using System.Reflection;
using Microsoft.VisualStudio.Shell;

namespace TSqlJump
{
    public static class SqlConnectionProvider
    {
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