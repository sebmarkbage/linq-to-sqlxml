using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Xml.Linq;

//order by documentdata.value('((/object/state/Name)[1])','nvarchar(MAX)')

namespace LinqToSqlXml
{
    public class DocumentQueryBuilder : ExpressionVisitor
    {
        private static readonly Dictionary<string, string> Functions = new Dictionary<string, string>
                                                                           {
                                                                               {"Sum", "fn:sum"},
                                                                               {"Max", "fn:max"},
                                                                               {"Min", "fn:min"},
                                                                               {"Average", "fn:avg"},
                                                                           };

        private static readonly Dictionary<ExpressionType, string> Operators =
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
        public bool IsProjection;
        private string collectionName;
        public string[] columns = new[] {"*"};
        public string from = "from documents";
        private int limit = -1;
        public string orderby = "";
        private string where = "";
        private string wherepredicate = "";

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
                    return string.Format("{0} and {1}(documentdata.exist('/document[{2}]')) = 1", where,Environment.NewLine, wherepredicate);
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
            string tmp = paths.Pop();
            paths.Push("document[1]/");
            IsProjection = true;
            var unary = node.Arguments[1] as UnaryExpression;
            var lambda = unary.Operand as LambdaExpression;
            var selector1 = lambda.Body as NewExpression;
            var selector2 = lambda.Body as MemberInitExpression;

            const string format = "DocumentData.query('<document type=\"{0}\">{1}</document>') as DocumentData";

            var selector = "";
            columns = new string[2];
            if (selector1 != null)
            {
                string[] members = selector1.Members.Select(m => m.Name).ToArray();                                
                selector = BuildSelectors1(selector1, members, "document");
            }
            if (selector2 != null)
            {
                string[] members = selector2.Bindings.Select(m => m.Member.Name).ToArray();
                selector = BuildSelectors2(selector2, members, "document");
            }

            var x = XElement.Parse(selector);
            selector = x.ToString();

            columns = new string[2];
            columns[0] = "Id";
            columns[1] = "DocumentData.query('" + selector + "') as DocumentData";

            paths.Pop();
            paths.Push(tmp);
            //columns = string.Join(",", members.Select(m => m.Name));
        }

        private string BuildSelectors2(MemberInitExpression selector, string[] members,string owner)
        {

            string projection = "";
            for (int i = 0; i < members.Length; i++)
            {
                string member = members[i];

                var a = selector.Bindings[i] as MemberAssignment;
                Expression expression = a.Expression;
                string propertyContent = BuildSelector(expression);
                string propertyType = DocumentSerializer.GetSerializedTypeName(expression.Type);

                string propertyProjection = string.Format("<{0} type=\"{1}\">{{{2}}}</{0}>", member, propertyType,
                                                          propertyContent);
                projection += propertyProjection;
            }

            return string.Format("<{0} type=\"{1}\">{2}</{0}>",owner,selector.NewExpression.Type.SerializedName(), projection);
        }

        private string BuildSelectors1(NewExpression selector, string[] members,string owner)
        {
            string projection = "";
            for (int i = 0; i < members.Length; i++)
            {
                string member = members[i];
                Expression expression = selector.Arguments[i];
                string propertyContent = BuildSelector(expression);
                string propertyType = DocumentSerializer.GetSerializedTypeName(expression.Type);

                string propertyProjection = string.Format("<{0} type=\"{1}\">{{{2}}}</{0}>", member, propertyType,
                                                          propertyContent);
                projection += propertyProjection;
            }
            return string.Format("<{0} type=\"dynamic\">{1}</{0}>",owner,projection);
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
            var binaryExpression = expression as BinaryExpression;
            string op = Operators[expression.NodeType];
            string left = BuildSelector(binaryExpression.Left);

            var rightAsUnary = binaryExpression.Right as UnaryExpression;
            ConstantExpression rightAsConstant = rightAsUnary != null
                                                     ? rightAsUnary.Operand as ConstantExpression
                                                     : null;
            if (rightAsConstant != null && rightAsConstant.Value == null)
            {
                return string.Format("{0}[@type{1}\"null\"]", left, op);
            }

            string right = BuildSelector(binaryExpression.Right);
            return string.Format("({0} {1} {2})", left, op, right);
        }

        private string BuildSelectorTypeIs(Expression expression)
        {
            throw new NotImplementedException();
        }

        private string BuildSelectorMemberAccess(Expression expression)
        {
            string result = BuildSelectorMemberAccessRec(expression);

            if (expression.Type == typeof (string))
                return string.Format("xs:string({0})", result);

            if (expression.Type == typeof (Guid))
                return string.Format("xs:string({0})", result);

            if (expression.Type == typeof (int))
                return string.Format("xs:int({0})", result);

            if (expression.Type == typeof (decimal))
                return string.Format("xs:decimal({0})", result);

            if (expression.Type == typeof (double))
                return string.Format("xs:double({0})", result);

            if (typeof (IEnumerable).IsAssignableFrom(expression.Type))
                return string.Format("{0}/element", result);

            return result;
        }

        private string BuildSelectorMemberAccessRec(Expression expression)
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

            if (methodCallExpression.Method.DeclaringType == typeof (Enumerable) ||
                methodCallExpression.Method.DeclaringType == typeof (Queryable))
            {
                switch (methodCallExpression.Method.Name)
                {
                    case "Select":
                        return BuildSelectorProjection(methodCallExpression);
                    case "Any":
                        return BuildSelectorAny(methodCallExpression);
                    case "Sum":
                    case "Min":
                    case "Max":
                    case "Average":
                        return BuildSelectorAggregate(methodCallExpression,
                                                      Functions[methodCallExpression.Method.Name]);
                    default:
                        break;
                }
            }

            throw new NotSupportedException("Unknown method");
        }

        private string BuildSelectorProjection(MethodCallExpression methodCallExpression)
        {
            string propertyPath = BuildSelector(methodCallExpression.Arguments[0]);
            var variable = GetFreeVariable();
            paths.Push(variable + "/");
            var lambda = methodCallExpression.Arguments[1] as LambdaExpression;

            var selector1 = lambda.Body as NewExpression;
            var selector2 = lambda.Body as MemberInitExpression;
            string result = "";
            if (selector1 != null)
            {
                string[] members = selector1.Members.Select(m => m.Name).ToArray();
                result= BuildSelectors1(selector1, members,"element");
            }

            if (selector2 != null)
            {
                string[] members = selector2.Bindings.Select(m => m.Member.Name).ToArray();
                result=  BuildSelectors2(selector2, members,"element");
            }
            paths.Pop();
            return string.Format("for {0} in {1} return {2}",variable,propertyPath,result);
        }

        private string BuildSelectorAny(MethodCallExpression methodCallExpression)
        {
            throw new NotImplementedException();
        }

        private string BuildSelectorAggregate(MethodCallExpression methodCallExpression, string functionName)
        {
            string propertyPath = BuildSelector(methodCallExpression.Arguments[0]);
            var lambda = methodCallExpression.Arguments[1] as LambdaExpression;
            Expression body = lambda.Body;
            string freeVariable = GetFreeVariable();
            paths.Push(freeVariable + "/");
            string part = BuildPredicate(body);
            paths.Pop();
            string predicate = string.Format("{0}( for {1} in {2} return xs:decimal({3}))", functionName,
                                             freeVariable,
                                             propertyPath,
                                             part);
            return predicate;
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
                                                       Functions[methodCallExpression.Method.Name]);
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
            string op = Operators[expression.NodeType];
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