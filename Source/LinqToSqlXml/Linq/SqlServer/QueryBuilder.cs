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
        private string collectionName;
        public string[] columns = new[] {"*"};
        public string from = "from documents";
        private int limit = -1;
        public string orderby = "";
        private string where = "";
        private string wherepredicate = "";

        public QueryBuilder(string collectionName)
        {
            this.collectionName = collectionName;
            where = string.Format("where CollectionName = '{0}' ", collectionName);
            paths.Push("");
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

        private readonly Stack<string> paths = new Stack<string>();
        private string CurrentPath
        {
            get { return paths.Peek(); }
        }
        private string GetFreeVariable()
        {
            int index = paths.Count;
            return "$" + ((char)(64 + index));
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
                        TranslateToWhere(node);
                        break;
                    case "OfType":
                        TranslateToOfType(node);
                        break;
                    case "Take":
                        TranslateToTake(node);
                        break;
                    case "Select":
                        columns = selectorBuilder.TranslateToProjection(node);
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
                if (memberExpression.Expression is MemberExpression)
                    prev = BuildPredicate(memberExpression.Expression);

                return prev + current;
            }
            throw new NotSupportedException("Unknown order by clause");
        }

        private void TranslateToWhere(MethodCallExpression node)
        {
            if (node.Arguments.Count != 2)
                return;

            Expression predicate = node.Arguments[1];
            var x = predicate as UnaryExpression;
            Expression operand = x.Operand;

            if (wherepredicate != "")
                wherepredicate += " and " + Environment.NewLine;

            var lambdaExpression = operand as LambdaExpression;
            wherepredicate += BuildPredicate(lambdaExpression.Body);
        }

        private string BuildPredicate(Expression expression)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.Call:
                    return BuildPredicateCall(expression);
                case ExpressionType.Convert:
                    return BuildPredicateConvert(expression);
                case ExpressionType.Constant:
                    return BuildPredicateConstant(expression);
                case ExpressionType.MemberAccess:
                    return BuildPredicateMemberAccess(expression);
                case ExpressionType.TypeIs:
                    return BuildPredicateTypeIs(expression);
                case ExpressionType.AndAlso:
                case ExpressionType.OrElse:
                case ExpressionType.NotEqual:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                case ExpressionType.Equal:
                case ExpressionType.Add:
                case ExpressionType.Subtract:
                case ExpressionType.Multiply:
                case ExpressionType.Divide:

                    return BuildPredicateBinaryExpression(expression);
                default:
                    throw new NotSupportedException("Unknown expression type");
            }
        }

        private string BuildPredicateCall(Expression expression)
        {
            var methodCallExpression = expression as MethodCallExpression;

            if (methodCallExpression.Method.DeclaringType == typeof (Enumerable) ||
                methodCallExpression.Method.DeclaringType == typeof (Queryable))
            {
                switch (methodCallExpression.Method.Name)
                {
                    case "Any":
                        return BuildAnyPredicate(methodCallExpression);
                    case "Sum":
                    case "Min":
                    case "Max":
                    case "Average":
                        return BuildAggregatePredicate(methodCallExpression,
                                                       SqlServerXQuery.Functions[methodCallExpression.Method.Name]);
                    default:
                        break;
                }
            }

            throw new NotSupportedException("Unknown method");
        }

        private string BuildAnyPredicate(MethodCallExpression methodCallExpression)
        {
            string rootPath = BuildPredicate(methodCallExpression.Arguments[0]);
            var lambda = methodCallExpression.Arguments[1] as LambdaExpression;
            Expression body = lambda.Body;
            string part = BuildPredicate(body);
            string propertyPath = BuildPredicate(methodCallExpression.Arguments[0]);
            string predicate = string.Format("{0}[{1}]", propertyPath, part);
            return predicate;
        }

        private string BuildAggregatePredicate(MethodCallExpression methodCallExpression, string functionName)
        {
            string propertyPath = BuildPredicate(methodCallExpression.Arguments[0]);
            var lambda = methodCallExpression.Arguments[1] as LambdaExpression;
            Expression body = lambda.Body;
            string freeVariable = GetFreeVariable();
            paths.Push(freeVariable + "/");
            string part = BuildPredicate(body);
            paths.Pop();
            string predicate = string.Format("{0}( for {1} in {2}/element return {3})", functionName, freeVariable,
                                             propertyPath,
                                             part);
            return predicate;
        }



        private string BuildPredicateConvert(Expression expression)
        {
            var convertExpression = expression as UnaryExpression;
            return BuildPredicate(convertExpression.Operand);
        }

        private string BuildPredicateBinaryExpression(Expression expression)
        {
            var binaryExpression = expression as BinaryExpression;
            string op = SqlServerXQuery.Operators[expression.NodeType];
            string left = BuildPredicate(binaryExpression.Left);


            var rightAsUnary = binaryExpression.Right as UnaryExpression;
            ConstantExpression rightAsConstant = rightAsUnary != null
                                                     ? rightAsUnary.Operand as ConstantExpression
                                                     : null;
            if (rightAsConstant != null && rightAsConstant.Value == null)
            {
                return string.Format("{0}[@type{1}\"null\"]", left, op);
            }
            else
            {
                string right = BuildPredicate(binaryExpression.Right);
                return string.Format("({0} {1} {2})", left, op, right);
            }
        }

        private string BuildPredicateTypeIs(Expression expression)
        {
            var typeBinaryExpression = expression as TypeBinaryExpression;
            string left = BuildPredicate(typeBinaryExpression.Expression);
            string right = typeBinaryExpression.TypeOperand.SerializedName();
            return string.Format("(documentdata.exist('{0}/__meta/type/text()[. = \"{1}\"]') = 1)",
                                 left, right);
        }

        private string BuildPredicateMemberAccess(Expression expression)
        {
            var memberExpression = expression as MemberExpression;
            string memberName = memberExpression.Member.Name;

            if (memberExpression.Member.DeclaringType == typeof (DateTime))
            {
                if (memberName == "Now")
                    return string.Format("xs:dateTime(\"{0}\")", DocumentSerializer.SerializeDateTime(DateTime.Now));
            }


            string current = string.Format("{0}[1]", memberName);
            string prev = "";
            if (memberExpression.Expression is MemberExpression)
                prev = BuildPredicate(memberExpression.Expression);
            else
                prev = CurrentPath;

            return prev + current;
        }

        private string BuildPredicateConstant(Expression expression)
        {
            var constantExpression = expression as ConstantExpression;
            object value = constantExpression.Value;

            if (constantExpression.Type == typeof (string))
                return "\"" + constantExpression.Value + "\"";
            if (constantExpression.Type == typeof (int))
                return string.Format("xs:int({0})", DocumentSerializer.SerializeDecimal((int) value));
            if (constantExpression.Type == typeof (decimal))
                return string.Format("xs:decimal({0})", DocumentSerializer.SerializeDecimal((decimal) value));
            if (constantExpression.Type == typeof (DateTime))
                return string.Format("xs:dateTime({0})", DocumentSerializer.SerializeDateTime((DateTime) value));

            return constantExpression.Value.ToString();
        }

        internal string GetSelect()
        {
            string columns = string.Join(",", this.columns);
            if (limit != -1)
                return string.Format("select top {0} {1}", limit, columns);

            return string.Format("select {0}", columns);
        }
    }
}