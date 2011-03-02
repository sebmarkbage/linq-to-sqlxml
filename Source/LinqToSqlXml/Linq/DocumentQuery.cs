using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;

namespace LinqToSqlXml
{
    public class DocumentQuery<T> :   IQueryable<T> , IOrderedQueryable<T>
    {

        public DocumentQuery(DocumentQueryProvider queryProvider)
        {
            this.expression = System.Linq.Expressions.Expression.Constant(this);
            this.queryProvider = queryProvider;
        }

        public DocumentQuery(DocumentQueryProvider queryProvider, Expression expression)
        {
            this.expression = expression;
            this.queryProvider = queryProvider;
        }

        public IEnumerator<T> GetEnumerator()
        {
            var result = queryProvider.ExecuteQuery<T>(expression);
            var enumerator = ((IEnumerable<T>) result).GetEnumerator();
            return enumerator;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        Type IQueryable.ElementType
        {
            get { return typeof (T); }
        }

        private Expression expression;
        private DocumentQueryProvider queryProvider;
        Expression IQueryable.Expression
        {
            get { return expression; }
        }

        public IQueryProvider Provider
        {
            get { return queryProvider; }
        }
    }    
}
