using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Realtorist.Models.Helpers;

namespace Realtorist.DataAccess.Mongo.Serialization
{
    public class JTokenBsonSerializer : SerializerBase<JToken>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JTokenBsonSerializer"/> class.
        /// </summary>
        public JTokenBsonSerializer()
        {            
        }

        /// <summary>
        /// Deserializes a value.
        /// </summary>
        /// <param name="context">The deserialization context.</param>
        /// <param name="args">The deserialization args.</param>
        /// <returns>A deserialized value.</returns>
        public override JToken Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var bsonReader = context.Reader;

            var bsonType = bsonReader.CurrentBsonType;
            return JToken.Parse(bsonReader.ReadString());
        }

        /// <summary>
        /// Serializes a value.
        /// </summary>
        /// <param name="context">The serialization context.</param>
        /// <param name="args">The serialization args.</param>
        /// <param name="value">The object.</param>
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, JToken value)
        {
            context.Writer.WriteString(JsonConvert.SerializeObject(value));
            //BsonDocumentSerializer.Instance.Serialize(context, value.ToString());
        }
    }
}