using Microsoft.Kinect;
using Newtonsoft.Json;

namespace KinectMotion.Frames
{
   struct BodyIndexFrameData
   {
      public FrameDescription Description { get; set; }

      // Serialize this with custom converter to make sure that
      // it is serialized as array, not string.
      [JsonConverter(typeof(ByteArrayConverter))]
      public byte[] Pixels { get; set; }
   }
}
