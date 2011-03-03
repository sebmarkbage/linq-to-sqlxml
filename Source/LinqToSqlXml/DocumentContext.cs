namespace LinqToSqlXml
{
    public class DocumentContext
    {
        private readonly DocumentDataContext db = new DocumentDataContext();
        private readonly string dbInstance;

        public DocumentContext(string dbInstance)
        {
            this.dbInstance = dbInstance;
        }

        public string DbInstance
        {
            get { return dbInstance; }
        }

        internal DocumentDataContext DB
        {
            get { return db; }
        }

        public void EnsureDatabaseExists()
        {
            if (!db.DatabaseExists())
                db.CreateDatabase();
        }

        public DocumentCollection<T> GetCollection<T>() where T : class
        {
            return new DocumentCollection<T>(this);
        }

        public void SaveChanges()
        {
            db.SubmitChanges();
        }
    }
}