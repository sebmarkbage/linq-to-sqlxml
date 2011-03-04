using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;

namespace LinqToSqlXml
{
    public static class DocumentDeserializer
    {
        public static object Deserialize(XElement xml,Type expectedType)
        {
            //todo fix

// ReSharper disable PossibleNullReferenceException
            string typeName = xml.Attribute("type").Value;
// ReSharper restore PossibleNullReferenceException

            string value = xml.Value;
            if (typeName == "string")
                return DeserializeString(value);
            if (typeName == "guid")
                return DeserializeGuid(value);
            if (typeName == "bool")
                return DeserializeBool(value);
            if (typeName == "int")
                return DeserializeInt(value);
            if (typeName == "double")
                return DeserializeDouble(value);
            if (typeName == "decimal")
                return DeserializeDecimal(value);
            if (typeName == "datetime")
                return DeserializeDateTime(value);
            if (typeName == "null")
                return null;
            if (typeName == "collection")
            {
                var elementExpectedType = typeof(object);
                if (expectedType.IsGenericType)
                    elementExpectedType = expectedType.GetGenericArguments()[0];

                return xml.Elements().Select(e => Deserialize(e, elementExpectedType)).ToList();
            }


            Type type = Type.GetType(typeName);
            if (typeName == "dynamic")
                type = expectedType;

            if (type == null)
                return null;

            if (type.GetConstructor(new Type[] { }) == null)
            {
                //deserialize with ctor args
                
                var ctor = type.GetConstructors().First();
                var ctorArgs = ctor.GetParameters();
                var values = new object[ctorArgs.Length];
                int i=0;
                foreach (XElement xProperty in xml.Elements().Where(e => !e.Name.LocalName.StartsWith("__")))
                {
                    ParameterInfo parameter = ctorArgs.Where(p => p.Name == xProperty.Name).First();
                    var expectedPropertyType = parameter.ParameterType;

                    object parameterValue = Deserialize(xProperty, expectedPropertyType);
                    if (parameterValue is List<object>)
                    {
                        parameterValue = DeserializeList2(parameterValue, expectedPropertyType);
                    }
                    values[i] = parameterValue;
                    i++;
                    //values[parameter.Name] = parameterValue;
                }

                object result = Activator.CreateInstance(type, values);

                return result;
            }
            else
            {
                //deserialize with default ctor
                object result = Activator.CreateInstance(type, null);
                foreach (XElement xProperty in xml.Elements().Where(e => !e.Name.LocalName.StartsWith("__")))
                {
                    PropertyInfo property = type.GetProperty(xProperty.Name.LocalName);
                    if (property.CanWrite == false)
                        continue;
                    var expectedPropertyType = property.PropertyType;

                    object propertyValue = Deserialize(xProperty, expectedPropertyType);
                    if (propertyValue is List<object>)
                    {
                        DeserializeList(value, property, propertyValue, result);
                    }
                    else
                    {
                        property.SetValue(result, propertyValue, null);
                    }
                }
                return result;
            }
        }

        private static void DeserializeList(string value, PropertyInfo property, object propertyValue, object result)
        {
            var list = (List<object>) propertyValue;
            Type collectionType = property.PropertyType;
            if (collectionType.IsInterface && collectionType.IsGenericType)
            {
                Type elementType = collectionType.GetGenericArguments().First();
                Type concreteType = typeof (List<>).MakeGenericType(elementType);
                var concreteList = (IList) Activator.CreateInstance(concreteType);
                foreach (object item in list)
                    concreteList.Add(item);
                property.SetValue(result, concreteList, null);
            }
            else if (typeof (Array).IsAssignableFrom(collectionType))
            {
                int length = list.Count;
                Array arr = null;
                for (int i = 0; i < length; i++)
                {
                    arr.SetValue(list[i], i);
                }
                property.SetValue(result, arr, null);
            }
            else if (collectionType == typeof (object))
            {
                property.SetValue(result, value, null);
            }
        }

        private static object DeserializeList2(object propertyValue, Type collectionType)
        {
            var list = (List<object>)propertyValue;

            if (collectionType.IsInterface && collectionType.IsGenericType)
            {
                Type elementType = collectionType.GetGenericArguments().First();
                Type concreteType = typeof(List<>).MakeGenericType(elementType);
                var concreteList = (IList)Activator.CreateInstance(concreteType);
                foreach (object item in list)
                    concreteList.Add(item);

                return concreteList;
            }
            else if (typeof(Array).IsAssignableFrom(collectionType))
            {
                int length = list.Count;
                Array arr = null;
                for (int i = 0; i < length; i++)
                {
                    arr.SetValue(list[i], i);
                }
                return arr;
            }
            else if (collectionType == typeof(object))
            {
                throw new Exception();
            }

            throw new NotSupportedException("");
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