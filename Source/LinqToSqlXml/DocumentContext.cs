using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LinqToSqlXml;

namespace LinqToSqlXml
{
    public class DocumentContext
    {

        public void EnsureDatabaseExists()
        {
            if (!db.DatabaseExists())
                this.db.CreateDatabase();
        }

        public DocumentContext(string dbInstance)
        {
            this.dbInstance = dbInstance;
        }

        private DocumentDataContext db = new DocumentDataContext();
        private string dbInstance;
        
        public DocumentCollection<T> GetCollection<T>() where T : class
        {
            return new DocumentCollection<T>(this);
        }

        public string DbInstance { get { return dbInstance; } }
        internal DocumentDataContext DB
        {
            get { return db; }
        }

        public void SaveChanges()
        {
            db.SubmitChanges();
        }
    }

    
}
