using Microsoft.Kinect;
using System.Collections.Generic;

namespace KinectMotion.Models
{
   struct BodyModel
   {
      public PointF Lean { get; set; }

      public bool IsRestricted { get; set; }

      public bool IsTracked { get; set; }

      public ulong TrackingId { get; set; }

      public FrameEdges ClippedEdges { get; set; }

      public IReadOnlyDictionary<JointType, Joint> Joints { get; set; }

      public HandState HandRightState { get; set; }

      public TrackingConfidence HandLeftConfidence { get; set; }

      public HandState HandLeftState { get; set; }

      public IReadOnlyDictionary<JointType, JointOrientation> JointOrientations { get; set; }

      public TrackingState LeanTrackingState { get; set; }

      public TrackingConfidence HandRightConfidence { get; set; }

      public IReadOnlyDictionary<JointType, DepthSpacePoint> JointScreenPositions { get; set; }
   }
}
