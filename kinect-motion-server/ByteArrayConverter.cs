using Newtonsoft.Json;
using System;

namespace KinectMotion
{
   class ByteArrayConverter : JsonConverter
   {
      public override bool CanConvert(Type objectType)
      {
         return objectType == typeof(byte[]);
      }

      public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
      {
         if (value == null)
         {
            writer.WriteNull();
            return;
         }

         writer.WriteStartArray();

         var bytes = (byte[])value;

         foreach (var @byte in bytes)
            writer.WriteValue(@byte);

         writer.WriteEndArray();
      }

      public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
      {
         throw new NotImplementedException();
      }
   }
}
