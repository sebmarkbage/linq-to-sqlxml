using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Xml.Linq;

namespace LinqToSqlXml.SqlServer
{
    public partial class QueryBuilder : ExpressionVisitor
    {
        private SelectorBuilder selectorBuilder = new SelectorBuilder();
        private PredicateBuilder predicateBuilder = new PredicateBuilder();
        private string collectionName;
        public string from = "from documents";
        private int limit = -1;
        public string orderby = "";
        private string where = "";
        private string wherepredicate = "";
        private string documentDataSelector = "DocumentData"; //just the column by default

        public QueryBuilder(string collectionName)
        {
            this.collectionName = collectionName;
            where = string.Format("where CollectionName = '{0}' ", collectionName);
            
        }

        public string Where
        {
            get
            {
                if (wherepredicate != "")
                    return string.Format("{0} and {1}(documentdata.exist('/document[{2}]')) = 1", where,Environment.NewLine, wherepredicate);
                else
                    return where;
            }
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof (Queryable))
            {
                switch (node.Method.Name)
                {
                    case "OrderBy":
                        TranslateToOrderBy(node);
                        break;
                    case "Where":
                        {
                            if (wherepredicate != "")
                                wherepredicate += " and " + Environment.NewLine;
                            wherepredicate +=  predicateBuilder.TranslateToWhere(node);
                        break;
                        }
                    case "OfType":
                        TranslateToOfType(node);
                        break;
                    case "Take":
                        TranslateToTake(node);
                        break;
                    case "Select":
                        documentDataSelector = selectorBuilder.TranslateToProjection(node);
                        break;
                    default:
                        throw new NotSupportedException(string.Format("Method {0} is not yet supported",
                                                                      node.Method.Name));
                }
            }
            return base.VisitMethodCall(node);
        }

        private void TranslateToTake(MethodCallExpression node)
        {
            limit = (int) (node.Arguments[1] as ConstantExpression).Value;
        }

        private void TranslateToOfType(MethodCallExpression node)
        {
            var typeExpression = node.Arguments[0] as ConstantExpression;
            object value = typeExpression.Value;
            var queryable = value as IQueryable;
            Type ofType = queryable.ElementType;

            where += " and " + Environment.NewLine;
            string typeName = ofType.SerializedName();
            string query = string.Format("(documentdata.exist('/document/__meta/type/text()[. = \"{0}\"]') = 1)",
                                         typeName);
            where += query;
        }

        private void TranslateToOrderBy(MethodCallExpression node)
        {
            if (node.Arguments.Count != 2)
                return;

            Expression path = node.Arguments[1];
            var x = path as UnaryExpression;
            Expression operand = x.Operand;

            if (orderby != "")
                orderby += " , ";
            else
                orderby = "order by ";

            var lambdaExpression = operand as LambdaExpression;
            orderby += BuildOrderByStart(lambdaExpression.Body);
        }

        private string BuildOrderByStart(Expression expression)
        {
            string path = BuildOrderBy(expression);
            return string.Format("documentdata.value('(({0})[1])','nvarchar(MAX)')", path);
        }

        private string BuildOrderBy(Expression expression)
        {
            var memberExpression = expression as MemberExpression;
            if (memberExpression != null)
            {
                string memberName = memberExpression.Member.Name;
                string current = string.Format("/{0}", memberName);
                string prev = "";
        //        if (memberExpression.Expression is MemberExpression)
        //            prev = BuildPredicate(memberExpression.Expression);

                return prev + current;
            }
            throw new NotSupportedException("Unknown order by clause");
        }


        internal string GetSelect()
        {
            if (limit != -1)
                return string.Format("select top {0} Id,{1}", limit, documentDataSelector);

            return string.Format("select Id,{0}", documentDataSelector);
        }
    }
}