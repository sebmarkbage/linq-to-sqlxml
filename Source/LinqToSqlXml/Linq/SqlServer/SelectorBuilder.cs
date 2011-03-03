using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using System.Collections;
using System.Xml.Linq;

namespace LinqToSqlXml.SqlServer
{
    public class SelectorBuilder
    {
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

        public string TranslateToProjection(MethodCallExpression node)
        {
            paths.Push("document/");
            var unary = node.Arguments[1] as UnaryExpression;
            var lambda = unary.Operand as LambdaExpression;
            var selector1 = lambda.Body as NewExpression;
            var selector2 = lambda.Body as MemberInitExpression;

            var selector = "";
            var columns = new string[2];
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

            var result = "DocumentData.query('" + selector + "') as DocumentData";

            paths.Pop();
            return result;
        }

        private string BuildSelectors2(MemberInitExpression selector, string[] members, string owner)
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

            return string.Format("<{0} type=\"{1}\">{2}</{0}>", owner, selector.NewExpression.Type.SerializedName(), projection);
        }

        private string BuildSelectors1(NewExpression selector, string[] members, string owner)
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
            return string.Format("<{0} type=\"dynamic\">{1}</{0}>", owner, projection);
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
            string op = XQueryMapping.Operators[expression.NodeType];
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
            var memberExpression = expression as MemberExpression;
            string memberName = memberExpression.Member.Name;

            if (memberExpression.Member.DeclaringType == typeof(DateTime))
            {
                if (memberName == "Now")
                    return string.Format("xs:dateTime(\"{0}\")", DocumentSerializer.SerializeDateTime(DateTime.Now));
            }

            string result = BuildSelectorMemberAccessRec(expression);
            result = string.Format("({0})[1]", result);

            if (expression.Type == typeof(string))
                return string.Format("xs:string({0})", result);

            if (expression.Type == typeof(Guid))
                return string.Format("xs:string({0})", result);

            if (expression.Type == typeof(int))
                return string.Format("xs:int({0})", result);

            if (expression.Type == typeof(decimal))
                return string.Format("xs:decimal({0})", result);

            if (expression.Type == typeof(double))
                return string.Format("xs:double({0})", result);

            if (typeof(IEnumerable).IsAssignableFrom(expression.Type))
                return string.Format("{0}/element", result);

            return result;
        }

        private string BuildSelectorMemberAccessRec(Expression expression)
        {
            var memberExpression = expression as MemberExpression;
            string memberName = memberExpression.Member.Name;
           
            string current = string.Format("{0}", memberName);
            string prev = "";
            if (memberExpression.Expression is MemberExpression)
                prev = BuildSelectorMemberAccessRec(memberExpression.Expression) + "/";
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
                    case "Select":
                        return BuildSelectorProjection(methodCallExpression);
                    case "Any":
                        return BuildSelectorAny(methodCallExpression);
                    case "Sum":
                    case "Min":
                    case "Max":
                    case "Average":
                        return BuildSelectorAggregate(methodCallExpression,
                                                      XQueryMapping.Functions[methodCallExpression.Method.Name]);
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
                result = BuildSelectors1(selector1, members, "element");
            }

            if (selector2 != null)
            {
                string[] members = selector2.Bindings.Select(m => m.Member.Name).ToArray();
                result = BuildSelectors2(selector2, members, "element");
            }
            paths.Pop();
            return string.Format("for {0} in {1} return {2}", variable, propertyPath, result);
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
            string part = BuildSelector(body);
            paths.Pop();
            string predicate = string.Format("{0}( for {1} in {2} return xs:decimal({3}))", functionName,
                                             freeVariable,
                                             propertyPath,
                                             part);
            return predicate;
        }

    }
}
