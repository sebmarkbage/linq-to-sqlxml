using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;

namespace LinqToSqlXml.SqlServer
{
    public static class XQueryMapping
    {
        public static readonly Dictionary<ExpressionType, string> Operators = new Dictionary<ExpressionType, string>
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

        public static readonly Dictionary<string, string> Functions = new Dictionary<string, string>
                                                                           {
                                                                               {"Sum", "fn:sum"},
                                                                               {"Max", "fn:max"},
                                                                               {"Min", "fn:min"},
                                                                               {"Average", "fn:avg"},
                                                                           };

        public static readonly string xsTrue = "fn:true()";
        public static readonly string xsFalse = "fn:false()";

        public static string BuildLiteral(object value)
        {
            if (value is string)
                return "\"" + DocumentSerializer.SerializeString((string)value) + "\"";
            if (value is int)
                return string.Format("xs:int({0})", DocumentSerializer.SerializeDecimal((int)value));
            if (value is decimal)
                return string.Format("xs:decimal({0})", DocumentSerializer.SerializeDecimal((decimal)value));
            if (value is DateTime)
                return string.Format("xs:dateTime({0})", DocumentSerializer.SerializeDateTime((DateTime)value));
            if (value is bool)
                if ((bool)value)
                    return XQueryMapping.xsTrue;
                else
                    return XQueryMapping.xsFalse;

            return value.ToString();
        }
    }
}
