using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Xml.Linq;
using LinqToSqlXml.SqlServer;

namespace LinqToSqlXml
{
    public class SqlServerQueryProvider : IQueryProvider
    {
        private readonly DocumentCollection documentCollection;

        public SqlServerQueryProvider(DocumentCollection documentCollection)
        {
            this.documentCollection = documentCollection;
        }

        #region IQueryProvider Members

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new DocumentQuery<TElement>(this, expression);
        }

        public IQueryable CreateQuery(Expression expression)
        {
            throw new NotImplementedException();
        }

        public TResult Execute<TResult>(Expression expression)
        {
            return default(TResult);
        }

        public object Execute(Expression expression)
        {
            throw new NotImplementedException();
        }

        #endregion

        private static IEnumerable<TResult> DocumentEnumerator<TResult>(IEnumerable<Document> documents)
        {
            return documents
                .Select(document => document.DocumentData)
                .Select(xml => (TResult) DocumentDeserializer.Deserialize(xml))
                .Where(result => result != null);
        }

        public IEnumerable<TResult> ExecuteQuery<TResult>(Expression expression)
        {
            var visitor = new QueryBuilder(documentCollection.CollectionName);
            visitor.Visit(expression);

            string sql =
                visitor.GetSelect() + Environment.NewLine +
                visitor.from + Environment.NewLine +
                visitor.Where + Environment.NewLine +
                visitor.orderby;

            IEnumerable<Document> result = documentCollection.ExecuteQuery(sql);
            return DocumentEnumerator<TResult>(result);
        }
    }
}