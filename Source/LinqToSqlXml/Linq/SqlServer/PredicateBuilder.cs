using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;

namespace LinqToSqlXml.SqlServer
{
    public class PredicateBuilder
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

        public string TranslateToWhere(MethodCallExpression node)
        {
            paths.Push("");

            if (node.Arguments.Count != 2)
                throw new NotSupportedException("Unknown Where call");

            Expression predicate = node.Arguments[1];
            var x = predicate as UnaryExpression;
            Expression operand = x.Operand;

            var lambdaExpression = operand as LambdaExpression;
            var result =  BuildPredicate(lambdaExpression.Body);
            return result;
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

            if (methodCallExpression.Method.DeclaringType == typeof(Enumerable) ||
                methodCallExpression.Method.DeclaringType == typeof(Queryable))
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
                prev = CurrentPath;

            return prev + current;
        }

        private string BuildPredicateConstant(Expression expression)
        {
            var constantExpression = expression as ConstantExpression;
            object value = constantExpression.Value;

            if (constantExpression.Type == typeof(string))
                return "\"" + constantExpression.Value + "\"";
            if (constantExpression.Type == typeof(int))
                return string.Format("xs:int({0})", DocumentSerializer.SerializeDecimal((int)value));
            if (constantExpression.Type == typeof(decimal))
                return string.Format("xs:decimal({0})", DocumentSerializer.SerializeDecimal((decimal)value));
            if (constantExpression.Type == typeof(DateTime))
                return string.Format("xs:dateTime({0})", DocumentSerializer.SerializeDateTime((DateTime)value));

            return constantExpression.Value.ToString();
        }
    }
}
