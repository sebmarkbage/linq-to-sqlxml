using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;

namespace LinqToSqlXml
{
    public static class SqlServerXQuery
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

    }
}
