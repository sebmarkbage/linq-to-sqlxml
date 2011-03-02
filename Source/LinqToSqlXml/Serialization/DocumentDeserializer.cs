using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Reflection;
using System.Collections;

namespace LinqToSqlXml
{
    public static class DocumentDeserializer
    {
        public static object Deserialize(XElement xml)
        {
            //todo fix
            string typeName = xml.Attribute("type").Value;
            Type type = Type.GetType(typeName);

            if (type == null)
                return null;

            object result = Activator.CreateInstance(type, null);
            foreach (XElement xProperty in xml.Element("state").Elements())
            {
                PropertyInfo property = type.GetProperty(xProperty.Name.LocalName);
                if (property.CanWrite == false)
                    continue;

                object value = ResolvePropertyValue(xProperty);
                if (value is List<object>)
                {
                    List<object> list = (List<object>) value;
                    Type collectionType = property.PropertyType;
                    if (collectionType.IsInterface && collectionType.IsGenericType)
                    {
                        Type elementType = collectionType.GetGenericArguments().First();
                        var concreteType = typeof (List<>).MakeGenericType(elementType);
                        var concreteList = (IList)Activator.CreateInstance(concreteType);
                        foreach (var item in list)
                            concreteList.Add(item);
                        property.SetValue(result,concreteList,null);
                    }
                    else if (typeof(Array).IsAssignableFrom(collectionType))
                    {
                        var length = list.Count;
                        Array arr = null;
                        for (int i=0;i<length;i++)
                        {
                            arr.SetValue(list[i],i);
                        }
                        property.SetValue(result,arr,null);
                    }
                    else if (collectionType == typeof(object))
                    {
                        property.SetValue(result,value,null);
                    }
                }
                else
                {
                    property.SetValue(result, value, null);
                }
            }
            return result;
        }

        private static object ResolvePropertyValue(XElement xProperty)
        {
            string propertyType = xProperty.Attribute("type").Value;
            string propertyString = xProperty.Value;
            if (propertyType == "string")
                return DeserializeString(propertyString);
            if (propertyType == "guid")
                return DeserializeGuid(propertyString);
            if (propertyType == "bool")
                return DeserializeBool(propertyString);
            if (propertyType == "int")
                return DeserializeInt(propertyString);
            if (propertyType == "double")
                return DeserializeDouble(propertyString);
            if (propertyType == "decimal")
                return DeserializeDecimal(propertyString);
            if (propertyType == "datetime")
                return DeserializeDateTime(propertyString);
            if (propertyType == "null")
                return null;
            if (propertyType == "ref")
                return Deserialize((XElement)xProperty.FirstNode);
            if (propertyType == "collection")
            {
                var result = new List<object>();
                foreach (XElement xelement in xProperty.Elements())
                {
                    object element = Deserialize(xelement);
                    result.Add(element);
                }
                return result;
            }
            throw new NotSupportedException("Unknown property type");
        }

        private static object DeserializeDateTime(string propertyString)
        {
            return XmlConvert.ToDateTime(propertyString, XmlDateTimeSerializationMode.Local);
        }

        private static object DeserializeDecimal(string propertyString)
        {
            return XmlConvert.ToDecimal(propertyString);
        }

        private static object DeserializeDouble(string propertyString)
        {
            return XmlConvert.ToDouble(propertyString);
        }

        private static object DeserializeInt(string propertyString)
        {
            return XmlConvert.ToInt32(propertyString);
        }

        private static object DeserializeBool(string propertyString)
        {
            return XmlConvert.ToBoolean(propertyString);
        }

        private static object DeserializeGuid(string propertyString)
        {
            return XmlConvert.ToGuid(propertyString);
        }

        private static object DeserializeString(string propertyString)
        {
            return propertyString;
        }
    }
}
