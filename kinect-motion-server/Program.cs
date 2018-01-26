using KinectMotion.Frames;
using KinectMotion.Models;
using Microsoft.Kinect;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace KinectMotion
{
   class Program : IDisposable
   {
      const string WebSocketProtocol = "KinectMotionV1";

      KinectSensor Sensor { get; set; }

      BodyFrameReader BodyFrameReader { get; set; }

      BodyIndexFrameReader BodyIndexFrameReader { get; set; }

      WebSocketServer<ClientState> Server { get; set; }

      Queue<KeyValuePair<string, ArraySegment<byte>>> Queue { get; set; }

      JsonSerializerSettings SerializerSettings { get; set; }

      Body[] Bodies { get; set; }

      byte[] BodyIndexPixels { get; set; }

      Program()
      {
         Sensor = KinectSensor.GetDefault();
         BodyFrameReader = Sensor.BodyFrameSource.OpenReader();
         BodyIndexFrameReader = Sensor.BodyIndexFrameSource.OpenReader();
         Queue = new Queue<KeyValuePair<string, ArraySegment<byte>>>();
         SerializerSettings = new JsonSerializerSettings
         {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Culture = CultureInfo.InvariantCulture,
            Formatting = Formatting.None,
            Converters =
            {
               // Serialize enums as string names.
               new StringEnumConverter()
            }
         };

         // Streaming server will listen 8521 port and use WebSockets.
         Server = new WebSocketServer<ClientState>(8521, WebSocketProtocol);

         var bodyIndexFrameDescription = Sensor.BodyIndexFrameSource.FrameDescription;

         // Initialize body index pixel data.
         BodyIndexPixels = new byte[bodyIndexFrameDescription.Width * bodyIndexFrameDescription.Height];

         // We are interested in these Kinect frames. Body Index Frame contains
         // a raw body data. Body Frame contains the same data but in more structual
         // form.
         BodyIndexFrameReader.FrameArrived += BodyIndexFrameReader_FrameArrived;
         BodyFrameReader.FrameArrived += BodyFrameReader_FrameArrived;

         // Subscribe state changes of Kinect sensor and the streaming server.
         Sensor.IsAvailableChanged += Sensor_IsAvailableChanged;
         Server.ClientConnectionChanged += Server_ClientConnectionChanged;

         // Start server and Kinect sensor.
         Server.Start();
         Sensor.Open();
      }

      static void Main(string[] args)
      {
         using (var program = new Program())
         {
            Console.WriteLine(@"
 _  ___                 _   __  __       _   _             
| |/ (_)               | | |  \/  |     | | (_)            
| ' / _ _ __   ___  ___| |_| \  / | ___ | |_ _  ___  _ __  
|  < | | '_ \ / _ \/ __| __| |\/| |/ _ \| __| |/ _ \| '_ \ 
| . \| | | | |  __/ (__| |_| |  | | (_) | |_| | (_) | | | |
|_|\_\_|_| |_|\___|\___|\__|_|  |_|\___/ \__|_|\___/|_| |_|

Welcome to KinectMotion server! Valid WebSocket endpoints are:

{0}

The transportation protocol of WebSocket is {1}.

The server is running and accepting connections as long as this window is kept open. Please see examples in examples directory to understand better how this works.
", string.Join(Environment.NewLine, program.Server.Endpoints), WebSocketProtocol);

            // Stream the data as long as the application is running.
            program.Stream();
         }
      }

      void Stream()
      {
         lock (Queue)
         {

            // Stream until the server is alive.
            while (Server.KeepAlive(OnClientStateUpdate))
            {

               // Apply frame updates in order they have arrived.
               while (Queue.Count > 0)
               {
                  var item = Queue.Dequeue();

                  // Send updates to clients that are interested in this frame.
                  Server.SendAsync(item.Value, x => x.Types == null || x.Types.Contains(item.Key)).Wait();
               }

               // Wait for queue changes.
               Monitor.Wait(Queue);
            }
         }
      }

      ClientState OnClientStateUpdate(ArraySegment<byte> segment, ClientState previousState)
      {
         return FromBytes<ClientState>(segment);
      }

      void OnBodyData()
      {
         var description = Sensor.DepthFrameSource.FrameDescription;
         var cm = Sensor.CoordinateMapper;

         OnMessage(new ServerMessage<BodyFrameData>
         {
            Content = new BodyFrameData
            {
               ScreenDescription = description,
               Bodies = Bodies.Select(x => new BodyModel
                  {
                     Lean = x.Lean,
                     IsRestricted = x.IsRestricted,
                     IsTracked = x.IsTracked,
                     TrackingId = x.TrackingId,
                     ClippedEdges = x.ClippedEdges,
                     Joints = x.Joints,
                     HandRightState = x.HandRightState,
                     HandLeftConfidence = x.HandLeftConfidence,
                     HandLeftState = x.HandLeftState,
                     JointOrientations = x.JointOrientations,
                     LeanTrackingState = x.LeanTrackingState,
                     HandRightConfidence = x.HandRightConfidence,

                     // Convert all joint camera space points to depth space
                     // points that can be projected to 2D screen.
                     JointScreenPositions = x.Joints.ToDictionary(
                        y => y.Key,
                        y => cm.MapCameraPointToDepthSpace(new CameraSpacePoint
                           {
                              X = y.Value.Position.X,
                              Y = y.Value.Position.Y,

                              // Make sure that depth does not vanish.
                              Z = Math.Max(y.Value.Position.Z, 0.1f)
                           })
                     )
               })
            }
         });
      }

      void OnBodyIndexData()
      {
         var description = Sensor.BodyIndexFrameSource.FrameDescription;
         var cm = Sensor.CoordinateMapper;

         OnMessage(new ServerMessage<BodyIndexFrameData>
         {
            Content = new BodyIndexFrameData
            {
               Description = description,
               Pixels = BodyIndexPixels
            }
         });
      }

      void OnMessage<T>(ServerMessage<T> message)
      {
         var payload = ToBytes(message);

         // Put message to send queue and inform streaming thread that queue has changed.
         lock (Queue)
         {
            Queue.Enqueue(new KeyValuePair<string, ArraySegment<byte>>(message.Type, payload));
            Monitor.Pulse(Queue);
         }
      }

      void Server_ClientConnectionChanged(object sender, ClientConnectionChangedEventArgs<ClientState> e)
      {
         Console.WriteLine("There are {0} client connection(s).", e.Connections);
      }

      void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
      {
         if (e.IsAvailable)
            Console.WriteLine("Kinect sensor is now available.");
         else
            Console.WriteLine("Kinect sensor is not available.");
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

         OnBodyData();
      }

      void BodyIndexFrameReader_FrameArrived(object sender, BodyIndexFrameArrivedEventArgs e)
      {
         using (var frame = e.FrameReference.AcquireFrame())
         {
            if (frame == null)
               return;

            // Copy body index pixels to our memory.
            frame.CopyFrameDataToArray(BodyIndexPixels);
         }

         OnBodyIndexData();
      }

      ArraySegment<byte> ToBytes<T>(ServerMessage<T> message)
      {
         var array = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message, SerializerSettings));

         return new ArraySegment<byte>(array, 0, array.Length);
      }

      T FromBytes<T>(ArraySegment<byte> segment)
      {
         return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(segment.Array, segment.Offset, segment.Count));
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
}
