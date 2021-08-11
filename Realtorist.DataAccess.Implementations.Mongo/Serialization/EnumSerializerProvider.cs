using System;
using MongoDB.Bson.Serialization;

namespace Realtorist.DataAccess.Implementations.Mongo.Serialization
{
    /// <summary>
    /// Provides serializer for enum values
    /// </summary>
    public class EnumSerializerProvider : IBsonSerializationProvider
    {
        public IBsonSerializer GetSerializer(Type type)
        {
            if (!type.IsEnum) return null;
            var serializerType = typeof(EnumAsDisplayNameBsonSerializer<>).MakeGenericType(type);

            var serializer = Activator.CreateInstance(serializerType);
            return serializer as IBsonSerializer;
        }
    }
}