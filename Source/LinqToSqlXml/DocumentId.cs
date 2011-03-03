using System;
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
            PropertyInfo property = self.GetProperty("Id");
            if (property == null)
                return null;
            if (property.IsDefined(typeof (DocumentIdAttribute), true))
                return property;

            return null;
        }
    }
}