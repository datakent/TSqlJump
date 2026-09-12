namespace TSqlJump
{
    public sealed class SqlObjectInfo
    {
        public int ObjectId { get; set; }

        public string Database { get; set; }

        public string Schema { get; set; }

        public string ObjectName { get; set; }

        public string ObjectType { get; set; }

        public string ObjectTypeDescription { get; set; }

        public override string ToString()
        {
            return string.Format(
                "Database: {0}\r\n" +
                "Schema: {1}\r\n" +
                "Object: {2}\r\n" +
                "Type: {3}\r\n" +
                "Description: {4}\r\n" +
                "Object ID: {5}",
                Database,
                Schema,
                ObjectName,
                ObjectType,
                ObjectTypeDescription,
                ObjectId);
        }
    }
}