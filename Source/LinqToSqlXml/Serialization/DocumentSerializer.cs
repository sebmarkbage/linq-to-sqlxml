using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;

namespace LinqToSqlXml
{
    public static class DocumentSerializer
    {
        public static XElement Serialize(object item)
        {
            var objectTag = new XElement("object");
            Type itemType = item.GetType();
            objectTag.Add(new XAttribute("type", itemType.SerializedName()));

            var typesTag = new XElement("types");
            objectTag.Add(typesTag);
            Type currentType = itemType;
            while (currentType != null)
            {
                var typeTag = new XElement("type", currentType.SerializedName());
                typesTag.Add(typeTag);

                Type[] interfaces = currentType.GetInterfaces();
                foreach (Type interfaceType in interfaces)
                {
                    var interfaceTag = new XElement("type", interfaceType.SerializedName());
                    typesTag.Add(interfaceTag);
                }
                currentType = currentType.BaseType;
            }

            var stateTag = new XElement("state");
            objectTag.Add(stateTag);

            PropertyInfo[] properties = itemType.GetProperties();
            foreach (PropertyInfo property in properties)
            {
                object value = property.GetValue(item, null);
                if (value == null)
                {
                    var propertyTag = new XElement(property.Name);
                    propertyTag.Add(new XAttribute("type", "null"));
                    stateTag.Add(propertyTag);
                }
                else if (value is string)
                {
                    var propertyTag = new XElement(property.Name, SerializeString((string) value));
                    propertyTag.Add(new XAttribute("type", "string"));
                    stateTag.Add(propertyTag);
                }
                else if (value is Guid || value is Guid?)
                {
                    var propertyTag = new XElement(property.Name, SerializeGuid((Guid) value));
                    propertyTag.Add(new XAttribute("type", "guid"));
                    stateTag.Add(propertyTag);
                }
                else if (value is bool || value is bool?)
                {
                    var propertyTag = new XElement(property.Name, SerializeBool((bool) value));
                    propertyTag.Add(new XAttribute("type", "bool"));
                    stateTag.Add(propertyTag);
                }
                else if (value is decimal || value is decimal?)
                {
                    var propertyTag = new XElement(property.Name, SerializeDecimal((decimal) value));
                    propertyTag.Add(new XAttribute("type", "decimal"));
                    stateTag.Add(propertyTag);
                }
                else if (value is double || value is double?)
                {
                    var propertyTag = new XElement(property.Name, SerializeDouble((double) value));
                    propertyTag.Add(new XAttribute("type", "double"));
                    stateTag.Add(propertyTag);
                }
                else if (value is int || value is int?)
                {
                    var propertyTag = new XElement(property.Name, SerializeInt((int) value));
                    propertyTag.Add(new XAttribute("type", "int"));
                    stateTag.Add(propertyTag);
                }
                else if (value is DateTime || value is DateTime?)
                {
                    var propertyTag = new XElement(property.Name, SerializeDateTime((DateTime) value));
                    propertyTag.Add(new XAttribute("type", "datetime"));
                    stateTag.Add(propertyTag);
                }
                else if (value is IEnumerable)
                {
                    var propertyTag = new XElement(property.Name);
                    propertyTag.Add(new XAttribute("type", "collection"));
                    foreach (object childValue in (IList) value)
                    {
                        XElement collectionElement = Serialize(childValue);
                        propertyTag.Add(collectionElement);
                    }
                    stateTag.Add(propertyTag);
                }
                else if (value is Enum)
                {
                    var intValue = (int) value;
                    var propertyTag = new XElement(property.Name, intValue.ToString());
                    propertyTag.Add(new XAttribute("type", "int"));
                    stateTag.Add(propertyTag);
                }
                else
                {
                    var propertyTag = new XElement(property.Name);
                    propertyTag.Add(new XAttribute("type", "ref"));
                    XElement propertyReference = Serialize(value);
                    propertyTag.Add(propertyReference);
                    stateTag.Add(propertyTag);
                }
            }

            return objectTag;
        }

        public static string GetSerializedTypeName(Type type)
        {
            if (type == typeof(bool))
                return "bool";

            if (type == typeof(int))
                return "int";

            if (type == typeof(double))
                return "double";

            if (type == typeof(decimal))
                return "decimal";

            if (type == typeof(DateTime))
                return "datetime";

            if (type == typeof(string))
                return "string";

            if (type == typeof(Guid))
                return "guid";

            if (typeof(IEnumerable).IsAssignableFrom(type))
                return "collection";

            return "ref";
            throw new NotSupportedException("Unknown type");
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
            return string.Format("{0}, {1}", self.FullName, self.Assembly.FullName.Substring(0, self.Assembly.FullName.IndexOf(",")));
        }
    }
}