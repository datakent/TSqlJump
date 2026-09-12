using System;
using System.Data;

namespace TSqlJump
{
    public static class SqlObjectResolver
    {
        public static string GetObjectType(
            IDbConnection connection,
            SqlObjectReference reference)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            if (reference == null)
                throw new ArgumentNullException(nameof(reference));

            string database =
                string.IsNullOrWhiteSpace(reference.Database)
                    ? connection.Database
                    : reference.Database;

            string schema =
                string.IsNullOrWhiteSpace(reference.Schema)
                    ? "dbo"
                    : reference.Schema;

            bool openedByUs = false;
            string originalDatabase = connection.Database;

            try
            {
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                    openedByUs = true;
                }

                if (!string.Equals(
                        originalDatabase,
                        database,
                        StringComparison.OrdinalIgnoreCase))
                {
                    connection.ChangeDatabase(database);
                }

                const string sql = "SELECT o.type_desc FROM sys.objects AS o INNER JOIN sys.schemas AS s ON s.schema_id = o.schema_id WHERE s.name = @Schema AND o.name = @ObjectName AND o.is_ms_shipped = 0;";

                using (IDbCommand command =
                    connection.CreateCommand())
                {
                    command.CommandText = sql;

                    IDbDataParameter schemaParameter = command.CreateParameter();

                    schemaParameter.ParameterName = "@Schema";
                    schemaParameter.Value = schema;

                    command.Parameters.Add(schemaParameter);

                    IDbDataParameter objectParameter = command.CreateParameter();

                    objectParameter.ParameterName = "@ObjectName";
                    objectParameter.Value =
                        reference.ObjectName;

                    command.Parameters.Add(objectParameter);

                    object result = command.ExecuteScalar();

                    return result == null
                        ? null
                        : result.ToString();
                }
            }
            finally
            {
                if (!string.Equals(
                        connection.Database,
                        originalDatabase,
                        StringComparison.OrdinalIgnoreCase))
                {
                    connection.ChangeDatabase(
                        originalDatabase);
                }

                if (openedByUs &&
                    connection.State != ConnectionState.Closed)
                {
                    connection.Close();
                }
            }
        }
    }
}