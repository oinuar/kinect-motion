using KinectMotion.Models;
using Microsoft.Kinect;
using System.Collections.Generic;

namespace KinectMotion.Frames
{
   struct BodyFrameData
   {
      public FrameDescription ScreenDescription { get; set; }

      public IEnumerable<BodyModel> Bodies { get; set; }
   }
}
