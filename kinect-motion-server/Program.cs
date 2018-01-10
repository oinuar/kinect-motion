using Microsoft.Kinect;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace KinectMotion
{
   class Program : IDisposable
   {
      KinectSensor Sensor { get; set; }

      BodyFrameReader BodyFrameReader { get; set; }

      BodyIndexFrameReader BodyIndexFrameReader { get; set; }

      WebSocketServer Server { get; set; }

      Queue<byte[]> Queue { get; set; }

      JsonSerializerSettings SerializerSettings { get; set; }

      Body[] Bodies { get; set; }

      BodyIndex BodyIndex { get; set; }

      Program()
      {
         Sensor = KinectSensor.GetDefault();
         BodyFrameReader = Sensor.BodyFrameSource.OpenReader();
         BodyIndexFrameReader = Sensor.BodyIndexFrameSource.OpenReader();
         Queue = new Queue<byte[]>();
         SerializerSettings = new JsonSerializerSettings
         {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Culture = CultureInfo.InvariantCulture,
            Formatting = Formatting.None
         };

         // Streaming server will listen 8521 port and use WebSockets.
         Server = new WebSocketServer(8521);

         var bodyIndexFrameDescription = Sensor.BodyIndexFrameSource.FrameDescription;

         // Body index object that contains dimensions alongside with pixel data.
         BodyIndex = new BodyIndex
         {
            Width = bodyIndexFrameDescription.Width,
            Height = bodyIndexFrameDescription.Height,
            Pixels = new byte[bodyIndexFrameDescription.Width * bodyIndexFrameDescription.Height]
         };

         // We are interested in these Kinect frames. Body Index Frame contains
         // a raw body data. Body Frame contains the same data but in more structual
         // form.
         BodyIndexFrameReader.FrameArrived += BodyIndexFrameReader_FrameArrived;
         BodyFrameReader.FrameArrived += BodyFrameReader_FrameArrived;

         Server.ClientConnected += Server_ClientConnected;

         // Start server and Kinect sensor.
         Server.Start();
         Sensor.Open();
      }

      private void Server_ClientConnected(object sender, ClientConnectionEventArgs e)
      {
         Console.WriteLine("Client connected. Currently there are {0} connection(s).", e.Connections);
      }

      static void Main(string[] args)
      {
         using (var program = new Program())
         {
            Console.WriteLine("Serving on {0} endpoint(s).", string.Join(", ", program.Server.Endpoints));

            // Stream the data as long as the application is running.
            program.Stream();
         }
      }

      void Stream()
      {
         lock (Queue)
         {

            // Stream until the server is alive.
            while (Server.KeepAlive())
            {

               // Apply frame updates in order they have arrived.
               while (Queue.Count > 0)
                  Server.Send(Queue.Dequeue()).Wait();

               Monitor.Wait(Queue);
            }
         }
      }

      void BodyFrameReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
      {
         using (var frame = e.FrameReference.AcquireFrame())
         {
            if (frame == null)
               return;

            // Create or refresh body container.
            if (Bodies == null || frame.BodyCount != Bodies.Length)
               Bodies = new Body[frame.BodyCount];

            // Update the body data.
            frame.GetAndRefreshBodyData(Bodies);
         }

         // Create JSON payload of the data.
         var payload = ToJson(new Payload(Bodies, BodyIndex));

         // Put data to send queue as soon as it arrives.
         lock (Queue)
         {
            Queue.Enqueue(payload);
            Monitor.Pulse(Queue);
         }
      }

      void BodyIndexFrameReader_FrameArrived(object sender, BodyIndexFrameArrivedEventArgs e)
      {
         using (var frame = e.FrameReference.AcquireFrame())
         {
            if (frame == null)
               return;

            // Copy body index pixels to our memory. This itself won't update
            // connected clients; instead we wait for parsed and analyzed
            // body data to arrive from Kinect and update only then.
            frame.CopyFrameDataToArray(BodyIndex.Pixels);
         }
      }

      byte[] ToJson<T>(T value)
      {
         return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value, SerializerSettings));
      }

      public void Dispose()
      {
         if (Server != null)
            Server.Dispose();

         if (BodyFrameReader != null)
            BodyFrameReader.Dispose();

         if (BodyIndexFrameReader != null)
            BodyIndexFrameReader.Dispose();

         if (Sensor != null)
            Sensor.Close();

         Server = null;
         BodyFrameReader = null;
         BodyIndexFrameReader = null;
         Sensor = null;
      }
   }

   struct Payload
   {
      public Body[] Bodies { get; set; }

      public BodyIndex BodyIndex { get; set; }

      public Payload(Body[] bodies, BodyIndex bodyIndex)
      {
         Bodies = bodies;
         BodyIndex = bodyIndex;
      }
   }

   struct BodyIndex
   {
      public int Width { get; set; }

      public int Height { get; set; }

      [JsonConverter(typeof(ByteArrayConverter))]
      public byte[] Pixels { get; set; }
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
