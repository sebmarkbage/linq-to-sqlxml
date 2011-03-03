using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;

namespace LinqToSqlXml
{
    public static class DocumentSerializer
    {
        public static XElement Serialize(object item)
        {
            var root = new XElement("document");
            Serialize(item, root);
            return root;
        }


        private static void Serialize(object value, XElement ownerTag)
        {
            if (value == null)
            {
                ownerTag.Add(new XAttribute("type", "null"));
                return;
            }
            if (value is string)
            {
                ownerTag.Add(new XAttribute("type", "string"));
                ownerTag.Add(SerializeString((string) value));
                return;
            }
            if (value is Guid || value is Guid?)
            {
                ownerTag.Add(new XAttribute("type", "guid"));
                ownerTag.Add(SerializeGuid((Guid) value));
                return;
            }
            if (value is bool || value is bool?)
            {
                ownerTag.Add(new XAttribute("type", "bool"));
                ownerTag.Add(SerializeBool((bool) value));
                return;
            }
            if (value is decimal || value is decimal?)
            {
                ownerTag.Add(new XAttribute("type", "decimal"));
                ownerTag.Add(SerializeDecimal((decimal) value));
                return;
            }
            if (value is double || value is double?)
            {
                ownerTag.Add(new XAttribute("type", "double"));
                ownerTag.Add(SerializeDouble((double) value));
                return;
            }
            if (value is int || value is int?)
            {
                ownerTag.Add(new XAttribute("type", "int"));
                ownerTag.Add(SerializeInt((int) value));
                return;
            }
            if (value is DateTime || value is DateTime?)
            {
                ownerTag.Add(new XAttribute("type", "datetime"));
                ownerTag.Add(SerializeDateTime((DateTime) value));
                return;
            }
            if (value is IEnumerable)
            {
                ownerTag.Add(new XAttribute("type", "collection"));
                foreach (object childValue in (IList) value)
                {
                    var collectionElement = new XElement("element");
                    Serialize(childValue, collectionElement);
                    ownerTag.Add(collectionElement);
                }
                return;
            }
            if (value is Enum)
            {
// ReSharper disable PossibleInvalidCastException
                var intValue = (int) value;
// ReSharper restore PossibleInvalidCastException
                ownerTag.Add(new XAttribute("type", "int"));
                ownerTag.Add(SerializeInt(intValue));
                return;
            }
            Type itemType = value.GetType();
            ownerTag.Add(new XAttribute("type", itemType.SerializedName()));
            var metaTags = new XElement("__meta");

            Type currentType = itemType;
            while (currentType != null && currentType != typeof (object))
            {
                if (currentType != itemType)
                {
                    var typeTag = new XElement("type", currentType.SerializedName());
                    metaTags.Add(typeTag);
                }

                Type[] interfaces = currentType.GetInterfaces();
                foreach (Type interfaceType in interfaces)
                {
                    var interfaceTag = new XElement("type", interfaceType.SerializedName());
                    metaTags.Add(interfaceTag);
                }
                currentType = currentType.BaseType;
            }

            //only add metadata if metadata exists
            if (metaTags.Elements().Count() > 0)
                ownerTag.Add(metaTags);

            PropertyInfo[] properties = itemType.GetProperties();
            foreach (PropertyInfo property in properties)
            {
                var propertyTag = new XElement(property.Name);
                object propertyValue = property.GetValue(value, null);

                Serialize(propertyValue, propertyTag);
                ownerTag.Add(propertyTag);
            }
        }

        public static string GetSerializedTypeName(Type type)
        {
            if (type == typeof (bool))
                return "bool";

            if (type == typeof (int))
                return "int";

            if (type == typeof (double))
                return "double";

            if (type == typeof (decimal))
                return "decimal";

            if (type == typeof (DateTime))
                return "datetime";

            if (type == typeof (string))
                return "string";

            if (type == typeof (Guid))
                return "guid";

            if (typeof (IEnumerable).IsAssignableFrom(type))
                return "collection";

            return type.SerializedName();
        }

        public static string SerializeDateTime(DateTime value)
        {
            return XmlConvert.ToString(value, XmlDateTimeSerializationMode.Local);
        }

        public static string SerializeInt(int value)
        {
            return XmlConvert.ToString(value);
        }

        public static string SerializeDouble(double value)
        {
            return XmlConvert.ToString(value);
        }

        public static string SerializeDecimal(decimal value)
        {
            return XmlConvert.ToString(value);
        }

        public static string SerializeBool(bool value)
        {
            return XmlConvert.ToString(value);
        }

        public static string SerializeGuid(Guid value)
        {
            return XmlConvert.ToString(value);
        }

        public static string SerializeString(string value)
        {
            return value;
        }
    }

    public static class TypeExtensions
    {
        public static string SerializedName(this Type self)
        {
            return string.Format("{0}, {1}", self.FullName,
                                 self.Assembly.FullName.Substring(0, self.Assembly.FullName.IndexOf(",")));
        }
    }
}