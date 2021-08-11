using System;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Realtorist.Models.Helpers;

namespace Realtorist.DataAccess.Mongo.Serialization
{
    public class EnumAsDisplayNameBsonSerializer<TEnum> : StructSerializerBase<TEnum> where TEnum: struct, Enum
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EnumSerializer{TEnum}"/> class.
        /// </summary>
        public EnumAsDisplayNameBsonSerializer()
        {            
        }

        /// <summary>
        /// Deserializes a value.
        /// </summary>
        /// <param name="context">The deserialization context.</param>
        /// <param name="args">The deserialization args.</param>
        /// <returns>A deserialized value.</returns>
        public override TEnum Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var bsonReader = context.Reader;

            var bsonType = bsonReader.CurrentBsonType;
            switch (bsonType)
            {
                case BsonType.String:
                    return bsonReader.ReadString().GetEnumValueFromLookupDisplayText<TEnum>();
                case BsonType.Int32:
                    return Enum.GetValues<TEnum>().FirstOrDefault(x => Convert.ToInt32(x) == bsonReader.ReadInt32());
                case BsonType.Int64:
                    return Enum.GetValues<TEnum>().FirstOrDefault(x => Convert.ToInt64(x) == bsonReader.ReadInt64());
                default:
                    throw CreateCannotDeserializeFromBsonTypeException(bsonType);
            }
        }

        /// <summary>
        /// Serializes a value.
        /// </summary>
        /// <param name="context">The serialization context.</param>
        /// <param name="args">The serialization args.</param>
        /// <param name="value">The object.</param>
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TEnum value)
        {
            var bsonWriter = context.Writer;

            var val = value.GetLookupDisplayTextFromObject();
            bsonWriter.WriteString(val);
        }
    }
}