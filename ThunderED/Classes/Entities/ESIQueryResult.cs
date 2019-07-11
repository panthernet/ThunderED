namespace ThunderED.Classes.Entities
{
    public class ESIQueryResult<T>
        where T: class
    {
        public T Result;
        public QueryData Data = new QueryData();
    }

    public class QueryData
    {
        public string ETag;
        public string Message;
        public int ErrorCode;

        public bool IsNotModified => ErrorCode == 304;
        public bool IsNoConnection;
        public bool IsNotDeserialized => ErrorCode == -100;
        public bool IsFailed => ErrorCode != 0;
    }
}
