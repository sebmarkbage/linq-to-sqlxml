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
        public string limit = "";
        public string orderby = "";
        private string wherepredicate = "";
        public string documentDataSelector = "DocumentData"; //just the column by default

        public QueryBuilder()
        {
        }

        public string Where
        {
            get
            {
                if (wherepredicate != "")
                    return string.Format(" and (documentdata.exist('/document[{0}]')) = 1", wherepredicate);
                else
                    return "";
            }
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(Queryable))
            {
                switch (node.Method.Name)
                {
                    case "OrderBy":
                        TranslateToOrderBy(node);
                        break;
                    case "Where":
                        if (wherepredicate != "")
                            wherepredicate += " and " + Environment.NewLine;
                        wherepredicate += predicateBuilder.TranslateToWhere(node);
                        break;
                    case "OfType":
                        if (wherepredicate != "")
                            wherepredicate += " and " + Environment.NewLine;
                        wherepredicate += predicateBuilder.TranslateToOfType(node);
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
            limit = string.Format("top {0}", (int)(node.Arguments[1] as ConstantExpression).Value);
        }

        //move to predicate builder and fix it


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
    }
}