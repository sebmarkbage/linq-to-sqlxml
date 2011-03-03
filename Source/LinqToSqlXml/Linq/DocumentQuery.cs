using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace LinqToSqlXml
{
    public class DocumentQuery<T> : IQueryable<T>, IOrderedQueryable<T>
    {
        private readonly Expression expression;
        private readonly DocumentQueryProvider queryProvider;

        public DocumentQuery(DocumentQueryProvider queryProvider)
        {
            expression = Expression.Constant(this);
            this.queryProvider = queryProvider;
        }

        public DocumentQuery(DocumentQueryProvider queryProvider, Expression expression)
        {
            this.expression = expression;
            this.queryProvider = queryProvider;
        }

        #region IQueryable<T> Members

        public IEnumerator<T> GetEnumerator()
        {
            IEnumerable<T> result = queryProvider.ExecuteQuery<T>(expression);
            IEnumerator<T> enumerator = (result).GetEnumerator();
            return enumerator;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        Type IQueryable.ElementType
        {
            get { return typeof (T); }
        }

        Expression IQueryable.Expression
        {
            get { return expression; }
        }

        public IQueryProvider Provider
        {
            get { return queryProvider; }
        }

        #endregion
    }
}