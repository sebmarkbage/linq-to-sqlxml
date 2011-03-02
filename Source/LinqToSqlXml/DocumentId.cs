using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace LinqToSqlXml
{
    [AttributeUsage(AttributeTargets.Property)]
    public class DocumentIdAttribute : Attribute
    {
    }


    public static class DocumentIdExtensions
    {
        public static PropertyInfo GetDocumentIdProperty(this Type self)
        {
            var property = self.GetProperty("Id");
            if (property == null)
                return null;
            if (property.IsDefined(typeof(DocumentIdAttribute), true))
                return property;

            return null;
        }
    }
}
