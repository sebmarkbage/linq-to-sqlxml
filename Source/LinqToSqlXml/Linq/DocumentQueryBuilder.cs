using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Xml;
using System.Collections;

//order by documentdata.value('((/object/state/Name)[1])','nvarchar(MAX)')

namespace LinqToSqlXml
{
    public class DocumentQueryBuilder : ExpressionVisitor
    {
        private static readonly Dictionary<string, string> functions = new Dictionary<string, string>
                                                                    {
                                                                        {"Sum", "fn:sum"},
                                                                        {"Max", "fn:max"},
                                                                        {"Min", "fn:min"},
                                                                        {"Average", "fn:avg"},
                                                                    };

        private static bool IsAggregateFunction(string name)
        {
            return functions.ContainsKey(name);
        }

        private static readonly Dictionary<ExpressionType, string> operators =
            new Dictionary<ExpressionType, string>
                {
                    {ExpressionType.AndAlso, "and"},
                    {ExpressionType.OrElse, "or"},
                    {ExpressionType.NotEqual, "!="},
                    {ExpressionType.LessThan, "<"},
                    {ExpressionType.LessThanOrEqual, "<="},
                    {ExpressionType.GreaterThan, ">"},
                    {ExpressionType.GreaterThanOrEqual, ">="},
                    {ExpressionType.Equal, "="},
                    {ExpressionType.Add, "+"},
                    {ExpressionType.Subtract, "-"},
                    {ExpressionType.Divide, "/"},
                    {ExpressionType.Multiply, "*"},
                };

        private readonly Stack<string> paths = new Stack<string>();
        public bool IsProjection = false;
        private string collectionName;
        public string from = "from documents";
        public string orderby = "";
        public string[] columns = new string[] {"*"};
        private string where = "";
        private string wherepredicate = "";
        private int limit = -1;

        public DocumentQueryBuilder(string collectionName)
        {
            this.collectionName = collectionName;
            where = string.Format("where CollectionName = '{0}' ", collectionName);
            paths.Push("");
        }

        public string Where
        {
            get
            {
                if (wherepredicate != null)
                    return string.Format("{0} and (documentdata.exist('/object/state[{1}]')) = 1", where, wherepredicate);
                else
                    return where;
            }
        }

        private string CurrentPath
        {
            get { return paths.Peek(); }
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
                        TranslateToProjection(node);
                        break;
                    default:
                        throw new NotSupportedException(string.Format("Method {0} is not yet supported",
                                                                      node.Method.Name));
                }
            }
            return base.VisitMethodCall(node);
        }

        private void TranslateToProjection(MethodCallExpression node)
        {
            this.IsProjection = true;
            var unary = node.Arguments[1] as UnaryExpression;
            var lambda = unary.Operand as LambdaExpression;
            var selector1 = lambda.Body as NewExpression;
            var selector2 = lambda.Body as MemberInitExpression;
            if (selector1 != null)
            {
                var members = selector1.Members.Select(m => m.Name).ToArray();
                columns = new string[2];
                columns[0] = "Id";
                columns[1] = BuildSelectors1(selector1, members);
            }
            if (selector2 != null)
            {
                var members = selector2.Bindings.Select(m => m.Member.Name).ToArray();
                columns = new string[2];
                columns[0] = "Id";
                columns[1] = BuildSelectors2(selector2, members);
            }

            //columns = string.Join(",", members.Select(m => m.Name));
        }

        private string BuildSelectors2(MemberInitExpression selector, string[] members)
        {
            paths.Pop();
            paths.Push("/object[1]/state[1]/");
            string projection = "";
            for (int i = 0; i < members.Length; i++)
            {
                var member = members[i];

                var a = selector.Bindings[i] as MemberAssignment;
                Expression expression = a.Expression;
                var propertyContent = BuildSelector(expression);
                var propertyType = DocumentSerializer.GetSerializedTypeName(expression.Type);

                var propertyProjection = string.Format("<{0} type=\"{1}\">{{{2}}}</{0}>", member, propertyType, propertyContent);
                projection += propertyProjection;
            }
            return string.Format("DocumentData.query('<object type=\"{0}\"><state>{1}</state></object>') as DocumentData",selector.NewExpression.Type.SerializedName(), projection);
        }

        private string BuildSelectors1(NewExpression selector, string[] members)
        {
            paths.Pop();
            paths.Push("/object[1]/state[1]/");
            string projection = "";
            for (int i = 0; i < members.Length;i++ )
            {
                var member = members[i];
                var expression = selector.Arguments[i];
                var propertyContent = BuildSelector(expression);
                var propertyType = DocumentSerializer.GetSerializedTypeName(expression.Type);

                var propertyProjection = string.Format("<{0} type=\"{1}\">{{{2}}}</{0}>", member,propertyType, propertyContent);
                projection += propertyProjection;
            }
            return string.Format("DocumentData.query('<object type=\"dynamic\"><state>{0}</state></object>') as DocumentData", projection);
        }

        private string BuildSelector(Expression expression)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.Call:
                    return BuildSelectorCall(expression);
                case ExpressionType.Convert:
                    return BuildSelectorConvert(expression);
                case ExpressionType.Constant:
                    return BuildSelectorConstant(expression);
                case ExpressionType.MemberAccess:
                    return BuildSelectorMemberAccess(expression);
                case ExpressionType.TypeIs:
                    return BuildSelectorTypeIs(expression);
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
                    return BuildSelectorBinaryExpression(expression);
                default:
                    throw new NotSupportedException("Unknown expression type");
            }
        }

        private string BuildSelectorBinaryExpression(Expression expression)
        {
            throw new NotImplementedException();
        }

        private string BuildSelectorTypeIs(Expression expression)
        {
            throw new NotImplementedException();
        }

        private string BuildSelectorMemberAccess(Expression expression)
        {
            var result = BuildSelectorMemberAccessRec(expression);

            if (expression.Type == typeof(string))
                return string.Format("xs:string({0})",result);

            if (expression.Type == typeof(Guid))
                return string.Format("xs:string({0})", result);

            if (expression.Type == typeof(int))
                return string.Format("xs:int({0})", result);

            if (expression.Type == typeof(decimal))
                return string.Format("xs:decimal({0})", result);

            if (expression.Type == typeof(double))
                return string.Format("xs:double({0})", result);

            if (typeof(IEnumerable).IsAssignableFrom(expression.Type))
                return string.Format("{0}/object", result);

            return result;
        }

        private string BuildSelectorMemberAccessRec(Expression expression)
        {
            var memberExpression = expression as MemberExpression;
            string memberName = memberExpression.Member.Name;

            if (memberExpression.Member.DeclaringType == typeof(DateTime))
            {
                if (memberName == "Now")
                    return string.Format("xs:dateTime(\"{0}\")", DocumentSerializer.SerializeDateTime(DateTime.Now));
            }

            string current = string.Format("{0}[1]", memberName);
            string prev = "";
            if (memberExpression.Expression is MemberExpression)
                prev = BuildPredicate(memberExpression.Expression);
            else
            {
                return CurrentPath + current;
            }

            return prev + current;
        }

        private string BuildSelectorConstant(Expression expression)
        {
            throw new NotImplementedException();
        }

        private string BuildSelectorConvert(Expression expression)
        {
            throw new NotImplementedException();
        }

        private string BuildSelectorCall(Expression expression)
        {
            var methodCallExpression = expression as MethodCallExpression;

            if (methodCallExpression.Method.DeclaringType == typeof(Enumerable) ||
                methodCallExpression.Method.DeclaringType == typeof(Queryable))
            {
                switch (methodCallExpression.Method.Name)
                {
                    case "Any":
                        return BuildAnySelector(methodCallExpression);
                    case "Sum":
                    case "Min":
                    case "Max":
                    case "Average":
                        return BuildAggregateSelector(methodCallExpression,
                                                        functions[methodCallExpression.Method.Name]);
                    default:
                        break;
                }
            }

            throw new NotSupportedException("Unknown method");
        }

        private string BuildAnySelector(MethodCallExpression methodCallExpression)
        {
            throw new NotImplementedException();
        }

        private string BuildAggregateSelector(MethodCallExpression methodCallExpression, string functionName)
        {             
            string propertyPath = BuildSelector(methodCallExpression.Arguments[0]);
            var lambda = methodCallExpression.Arguments[1] as LambdaExpression;
            Expression body = lambda.Body;
            string freeVariable = GetFreeVariable();
            paths.Push(freeVariable + "/");
            string part = BuildPredicate(body);
            paths.Pop();
            string predicate = string.Format("{0}( for {1} in {2}/state[1] return xs:decimal({3}))", functionName, freeVariable,
                                             propertyPath,
                                             part);
            return predicate;
        }

        private void TranslateToTake(MethodCallExpression node)
        {
            var limit = (int)(node.Arguments[1] as ConstantExpression).Value;
            this.limit = limit;
        }

        private void TranslateToOfType(MethodCallExpression node)
        {
            var typeExpression = node.Arguments[0] as ConstantExpression;
            object value = typeExpression.Value;
            var queryable = value as IQueryable;
            Type ofType = queryable.ElementType;

            where += " and " + Environment.NewLine;
            string typeName = ofType.SerializedName();
            string query = string.Format("(documentdata.exist('/object/types/type/text()[. = \"{0}\"]') = 1)", typeName);
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
                string current = string.Format("/object/state/{0}", memberName);
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
                wherepredicate += " and ";

            var lambdaExpression = operand as LambdaExpression;
            wherepredicate += BuildPredicate(lambdaExpression.Body) ;
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
                                                        functions[methodCallExpression.Method.Name]);
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
            string predicate = string.Format("{0}/object/state[{1}]", propertyPath, part);
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
            string predicate = string.Format("{0}( for {1} in {2}/object/state return {3})", functionName, freeVariable,
                                             propertyPath,
                                             part);
            return predicate;
        }

        private string GetFreeVariable()
        {
            int index = paths.Count;
            return "$" + ((char) (64 + index));
        }

        private string BuildPredicateConvert(Expression expression)
        {
            var convertExpression = expression as UnaryExpression;
            return BuildPredicate(convertExpression.Operand);
        }

        private string BuildPredicateBinaryExpression(Expression expression)
        {
            var binaryExpression = expression as BinaryExpression;
            string op = operators[expression.NodeType];
            string left = BuildPredicate(binaryExpression.Left);


            var rightAsUnary = binaryExpression.Right as UnaryExpression;
            var rightAsConstant = rightAsUnary != null ? rightAsUnary.Operand as ConstantExpression : null;
            if (rightAsConstant != null && rightAsConstant.Value == null)
            {               
                return string.Format("{0}[@type{1}\"null\"]",left,op);
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
            return string.Format("(documentdata.exist('{0}/object/types/type/text()[. = \"{1}\"]') = 1)",
                                 left, right);
        }

        private string BuildPredicateMemberAccess(Expression expression)
        {
            var memberExpression = expression as MemberExpression;
            string memberName = memberExpression.Member.Name;

            if (memberExpression.Member.DeclaringType==typeof(DateTime))
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
                return string.Format("xs:int({0})", DocumentSerializer.SerializeDecimal((int)value));
            if (constantExpression.Type == typeof (decimal))
                return string.Format("xs:decimal({0})", DocumentSerializer.SerializeDecimal((decimal)value));
            if (constantExpression.Type == typeof(DateTime))
                return string.Format("xs:dateTime({0})", DocumentSerializer.SerializeDateTime((DateTime)value));

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