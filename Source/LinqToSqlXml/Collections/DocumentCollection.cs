using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using System.Data.SqlClient;



namespace LinqToSqlXml
{
    public class DocumentCollection
    {
        protected DocumentContext owner;
        protected string collectionName = "";

        internal IEnumerable<Document> ExecuteQuery(string query)
        {
            return this.owner.DB.ExecuteQuery<Document>(query);
        }

        

        public string CollectionName
        {
            get { return collectionName; }
        }
    }

    public class DocumentCollection<T> : DocumentCollection 
    {

        

        internal DocumentContext Owner
        {
            get { return owner; }
        }

        public DocumentCollection(DocumentContext owner)
        {
            this.owner = owner;
            this.collectionName = typeof(T).Name;
        }

        public void Add(T item)
        {
            Guid documentId;

            var idproperty = item.GetType().GetDocumentIdProperty();
            if (idproperty != null)
            {
                var propertyvalue = (Guid)idproperty.GetValue(item, null);
                if (propertyvalue == Guid.Empty)
                {
                    documentId = Guid.NewGuid();
                    idproperty.SetValue(item, documentId, null);
                }
                else
                    documentId = propertyvalue;
            }
            else
            {
                documentId = Guid.NewGuid();
            }


            Document doc = new Document();
            doc.Id = documentId;
            doc.DocumentData = DocumentSerializer.Serialize(item);
            doc.CollectionName = collectionName;
            doc.DbName = owner.DbInstance;

            owner.DB.Documents.InsertOnSubmit(doc);
        }

        public IQueryable<T> AsQueryable()
        {
            var queryProvider = new DocumentQueryProvider(this);
            return new DocumentQuery<T>(queryProvider);
        }
    }
}
