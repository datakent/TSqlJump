namespace TSqlJump
{
    public sealed class SqlObjectReference
    {
        public string Server { get; set; }
        public string Database { get; set; }
        public string Schema { get; set; }
        public string ObjectName { get; set; }

        public override string ToString()
        {
            return string.Format(
                "Server: {0}\r\nDatabase: {1}\r\nSchema: {2}\r\nObject: {3}",
                Server ?? "(current)",
                Database ?? "(current)",
                Schema ?? "(default)",
                ObjectName ?? "(none)");
        }
    }
}