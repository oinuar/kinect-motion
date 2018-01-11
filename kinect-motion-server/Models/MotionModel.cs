using Microsoft.Kinect;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace KinectMotion.Models
{
   struct MotionModel
   {
      public IEnumerable<BodyModel> Bodies { get; set; }

      // Serialize this with custom converter to make sure that
      // it is serialized as array, not string.
      [JsonConverter(typeof(ByteArrayConverter))]
      public byte[] BodyIndexPixels { get; set; }

      public FrameDescription DepthFrame { get; set; }

      public FrameDescription BodyIndexFrame { get; set; }
   }

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
