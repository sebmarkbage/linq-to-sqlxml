using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;

namespace LinqToSqlXml
{
    public class DocumentQueryProvider : IQueryProvider
    {
        private DocumentCollection documentCollection;

        public DocumentQueryProvider(DocumentCollection documentCollection)
        {
            this.documentCollection = documentCollection;
        }

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

        private IEnumerable<TResult> DocumentEnumerator<TResult>(IEnumerable<Document> documents)
        {
            foreach(var document in documents)
            {
                var xml = document.DocumentData;
                //only yield documents we can deserialize
                var result = (TResult)DocumentDeserializer.Deserialize(xml);
                if (result != null)
                    yield return result;
            }
        }

        public object Execute(Expression expression)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<TResult> ExecuteQuery<TResult>(Expression expression)
        {
            DocumentQueryBuilder visitor = new DocumentQueryBuilder(documentCollection.CollectionName);
            visitor.Visit(expression);

            var sql = 
                visitor.GetSelect() + Environment.NewLine + 
                visitor.from + Environment.NewLine + 
                visitor.Where + Environment.NewLine + 
                visitor.orderby;

            if (visitor.IsProjection)
            {
                var result = documentCollection.ExecuteQuery(sql);
                return DocumentEnumerator<TResult>(result);    
            }
            else
            {
                var result = documentCollection.ExecuteQuery(sql);
                return DocumentEnumerator<TResult>(result);    
            }            
        }
    }
}

