using System;
using MongoDB.Bson.Serialization;
using Newtonsoft.Json.Linq;

namespace Realtorist.DataAccess.Mongo.Serialization
{
    /// <summary>
    /// Provides serializer for enum values
    /// </summary>
    public class JTokenSerializerProvider : IBsonSerializationProvider
    {
        public IBsonSerializer GetSerializer(Type type)
        {
            if (type != typeof(JToken)) return null;
            return new JTokenBsonSerializer();
        }
    }
}