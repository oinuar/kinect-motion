import sys
import os
import bpy
from mathutils import *

# Add the directory of this file to search path in order to find the local packages.
sys.path.append(os.path.dirname(__file__))

from kinect_motion.client import Client

bl_info = {
   "name": "Kinect Motion",
   "description": "Reads Kinect Motion stream and does live mocap",
   "author": "Jussi Raunio",
   "version": (1, 0),
   "blender": (2, 79, 0),
   "location": "View3D > Toolbar > Kinect Motion",
   "category": "Animation",
   "wiki_url": "",
   "tracker_url": ""
   }

def on_capture_changed(self, context):

   # This will invoke the capture modal action. Note that since it is
   # a modal action, it handles its own state. Therefore, it knows when
   # the capture is toggled to false.
   if self.toggle_capture:
      bpy.ops.kinect_motion.capture("EXEC_DEFAULT")

   return None

def mocap_pose(position, orientation, armature_name, target_name, frame, insert_keyframe):

   # Manipulate armature pose bones.
   if armature_name in bpy.data.armatures and target_name in bpy.data.armatures[armature_name].pose.bones:
      bone = bpy.data.armatures[armature_name].pose.bones[target_name]

      # Set location & orientation. Note that bone location in pose mode usually does not affect many of the bones,
      # but this ensures that when affected, it is correct.
      bone.location = Vector((position["x"], position["y"], position["z"]))
      bone.rotation_quaternion = Quaternion((orientation["w"], orientation["x"], orientation["y"], orientation["z"]))

      # Insert bone keyframes to this frame if enabled.
      if insert_keyframe:
         bone.keyframe_insert(data_path = "location", frame = frame)
         bone.keyframe_insert(data_path = "rotation_quaternion", frame = frame)

class KinectMotionCaptureOperator(bpy.types.Operator):
   bl_idname = "kinect_motion.capture"
   bl_label = "Capture"
   bl_description = "Toggles Kinect Motion capturing"
   bl_options = { "REGISTER" }

   joint_map = {
      "spineBase": lambda x: x.spine_base_joint,
      "spineMid": lambda x: x.spine_mid_joint,
      "neck": lambda x: x.neck_joint,
      "head": lambda x: x.head_joint,
      "shoulderLeft": lambda x: x.shoulder_left_joint,
      "elbowLeft": lambda x: x.elbow_left_joint,
      "wristLeft": lambda x: x.wrist_left_joint,
      "handLeft": lambda x: x.hand_left_joint,
      "shoulderRight": lambda x: x.shoulder_right_joint,
      "elbowRight": lambda x: x.elbow_right_joint,
      "wristRight": lambda x: x.wrist_right_joint,
      "handRight": lambda x: x.hand_right_joint,
      "hipLeft": lambda x: x.hip_left_joint,
      "kneeLeft": lambda x: x.knee_left,
      "ankleLeft": lambda x: x.ankle_left_joint,
      "footLeft": lambda x: x.foot_left_joint,
      "hipRight": lambda x: x.hip_right_joint,
      "kneeRight": lambda x: x.knee_right_joint,
      "ankleRight": lambda x: x.ankle_right_joint,
      "footRight": lambda x: x.foot_right_joint,
      "spineShoulder": lambda x: x.spine_shoulder_joint,
      "handTipLeft": lambda x: x.hand_tip_left_joint,
      "thumbLeft": lambda x: x.thumb_left_joint,
      "handTipRight": lambda x: x.hand_tip_right_joint,
      "thumbRight": lambda x: x.thumb_right_joint
      }

   @classmethod
   def poll(cls, context):
      return context.window_manager.kinect_motion.toggle_capture

   def modal(self, context, event):

      # Cancel operation if ESC is pressed, capture is disabled or we are not in pose mode.
      if event.type == "ESC" or not context.window_manager.kinect_motion.toggle_capture or context.object.mode != "POSE":
         self.report({ "INFO" }, "Kinect Motion capture was stopped")
         return self.cancel(context)

      # The timer event ticks every frame.
      if event.type == "TIMER":
         preferences = context.user_preferences.addons[__name__].preferences
         mocap = context.scene.kinect_motion

         try:
            # Ensure that the connection is open.
            self.client.ensure_connected()

            # Process data stream once per frame. This will block if there are no
            # data ready.
            self.client.once()

            tracked_body = None

            # Find a tracked body from the stream.
            for body in self.client.bodies():

               # Skip all non-tracked bodies.
               if not body["isTracked"]:
                  continue

               # Report error if there are more than one tracked body.
               if tracked_body != None:
                  raise RuntimeError("Too many tracked bodies")

               tracked_body = body

            if tracked_body != None:

               # Accept this frame.
               self.frame += 1

               # Do live mocap of each body joint to their mapped counterparts.
               for joint, m in __class__.joint_map:
                  position = tracked_body["joints"][joint]["position"]
                  orientation = tracked_body["jointOrientations"][joint]["orientation"]

                  # Mocap this joint using a target armature and joint mapping function.
                  mocap_pose(position, orientation, mocap.armature, m(mocap), self.frame, preferences.auto_record_keyframes)

         except:

            # Cancel the operator and re-raise the exception. We could improve showing nice errors to
            # users but that's a different story.
            self.cancel(context)
            raise

      return { "PASS_THROUGH" }

   def execute(self, context):
      return self.invoke(context, None)

   def invoke(self, context, event):
      preferences = context.user_preferences.addons[__name__].preferences

      self.start_mode = context.object.mode

      # Switch to pose mode if not already set.
      if self.start_mode != "POSE" and not "FINISHED" in bpy.ops.object.mode_set(mode = "POSE"):
         return { "CANCELLED" }

      # Clear the state.
      self.timer = None
      self.frame = 0

      # Create a client that is used to read the stream.
      self.client = Client(preferences.endpoint)

      # Add a window timer which ticks for each rendering frame.
      self.timer = context.window_manager.event_timer_add(1 / context.scene.render.fps, context.window)

      # Let window manager to handle this operation.
      context.window_manager.modal_handler_add(self)

      return { "RUNNING_MODAL" }

   def cancel(self, context):
      context.window_manager.kinect_motion.toggle_capture = False

      if self.timer != None:
         context.window_manager.event_timer_remove(self.timer)
         self.timer = None

      if self.client != None:
         try:
            self.client.terminate()
         except:
            pass

         self.client = None

      # Restore object mode back to initial state.
      if self.start_mode != "POSE":
         bpy.ops.object.mode_set(mode = self.start_mode)

      self.start_mode = None

      return { "CANCELLED" }

class KinectMotionStreamPanel(bpy.types.Panel):
   bl_space_type = "VIEW_3D"
   bl_region_type = "TOOLS"
   bl_label = "Motion Stream"
   bl_category = "Kinect Motion"

   def draw(self, context):
      props = context.window_manager.kinect_motion
      preferences = context.user_preferences.addons[__name__].preferences

      row = self.layout.row()

      # Change button depending on if capture is toggled or not.
      if props.toggle_capture:
         row.prop(props, "toggle_capture", toggle = True, text = "Stop capture", icon = "ARMATURE_DATA")
      else:
         row.prop(props, "toggle_capture", toggle = True, text = "Start capture", icon = "POSE_DATA")

      row = self.layout.row()

      row.enabled = props.toggle_capture

      row.prop(preferences, "auto_record_keyframes")

      # Show warning if scene frame rate is not close to Kinect frame rate.
      if context.scene.render.fps < 29.97 or context.scene.render.fps > 30:
         row = self.layout.row()
         row.label(text = "It is recommended to set scene frame rate close to 30 frames/second since Kinect sensor outputs that frame rate.")

class KinectMotionBodyMocapPanel(bpy.types.Panel):
   bl_space_type = "VIEW_3D"
   bl_region_type = "TOOLS"
   bl_label = "Body Mocap"
   bl_category = "Kinect Motion"

   def draw(self, context):
      props = context.scene.kinect_motion

      self.__mocap_row(context).prop_search(props, "armature", bpy.data, "armatures")

      # Show bone selections only if armature selection is valid.
      if props.armature in bpy.data.armatures:
         self.__mocap_row(context).prop_search(props, "spine_base_joint", bpy.data.armatures[props.armature], "bones")
         self.__mocap_row(context).prop_search(props, "spine_mid_joint", bpy.data.armatures[props.armature], "bones")
         self.__mocap_row(context).prop_search(props, "neck_joint", bpy.data.armatures[props.armature], "bones")
         self.__mocap_row(context).prop_search(props, "head_joint", bpy.data.armatures[props.armature], "bones")
         self.__mocap_row(context).prop_search(props, "shoulder_left_joint", bpy.data.armatures[props.armature], "bones")
         self.__mocap_row(context).prop_search(props, "elbow_left_joint", bpy.data.armatures[props.armature], "bones")
         self.__mocap_row(context).prop_search(props, "wrist_left_joint", bpy.data.armatures[props.armature], "bones")
         self.__mocap_row(context).prop_search(props, "hand_left_joint", bpy.data.armatures[props.armature], "bones")
         self.__mocap_row(context).prop_search(props, "shoulder_right_joint", bpy.data.armatures[props.armature], "bones")
         self.__mocap_row(context).prop_search(props, "elbow_right_joint", bpy.data.armatures[props.armature], "bones")
         self.__mocap_row(context).prop_search(props, "wrist_right_joint", bpy.data.armatures[props.armature], "bones")
         self.__mocap_row(context).prop_search(props, "hand_right_joint", bpy.data.armatures[props.armature], "bones")
         self.__mocap_row(context).prop_search(props, "hip_left_joint", bpy.data.armatures[props.armature], "bones")
         self.__mocap_row(context).prop_search(props, "knee_left", bpy.data.armatures[props.armature], "bones")
         self.__mocap_row(context).prop_search(props, "ankle_left_joint", bpy.data.armatures[props.armature], "bones")
         self.__mocap_row(context).prop_search(props, "foot_left_joint", bpy.data.armatures[props.armature], "bones")
         self.__mocap_row(context).prop_search(props, "hip_right_joint", bpy.data.armatures[props.armature], "bones")
         self.__mocap_row(context).prop_search(props, "knee_right_joint", bpy.data.armatures[props.armature], "bones")
         self.__mocap_row(context).prop_search(props, "ankle_right_joint", bpy.data.armatures[props.armature], "bones")
         self.__mocap_row(context).prop_search(props, "foot_right_joint", bpy.data.armatures[props.armature], "bones")
         self.__mocap_row(context).prop_search(props, "spine_shoulder_joint", bpy.data.armatures[props.armature], "bones")
         self.__mocap_row(context).prop_search(props, "hand_tip_left_joint", bpy.data.armatures[props.armature], "bones")
         self.__mocap_row(context).prop_search(props, "thumb_left_joint", bpy.data.armatures[props.armature], "bones")
         self.__mocap_row(context).prop_search(props, "hand_tip_right_joint", bpy.data.armatures[props.armature], "bones")
         self.__mocap_row(context).prop_search(props, "thumb_right_joint", bpy.data.armatures[props.armature], "bones")

   def __mocap_row(self, context):
      row = self.layout.row()

      # Disable changing mocaps when capture is running.
      row.enabled = not context.window_manager.kinect_motion.toggle_capture

      return row

class KinectMotionAddonPreferences(bpy.types.AddonPreferences):
   bl_idname = __name__

   endpoint = bpy.props.StringProperty(
      name = "Stream endpoint",
      description = "Kinect Motion stream endpoint that provides the motion data",
      default = "ws://localhost:8521")

   auto_record_keyframes = bpy.props.BoolProperty(
      name = "Auto record",
      description = "When enabled, automatically record keyframes when body motion is detected",
      default = False)

   def draw(self, context):
      self.layout.row().label(text = "This addon requires that you run kinect-motion-server. If you do not have done so, please start it now.")
      self.layout.row().prop(self, "endpoint")

class KinectMotionWindowManagerPropertyGroup(bpy.types.PropertyGroup):
   toggle_capture = bpy.props.BoolProperty(
      name = "Toggle capture",
      description = "Tells whenever Kinect Motion capturing is enabled",
      default = False,
      update = on_capture_changed)

class KinectMotionScenePropertyGroup(bpy.types.PropertyGroup):
   armature = bpy.props.StringProperty(
      name = "Armature",
      description = "Armature whose bones are mapped")

   spine_base_joint = bpy.props.StringProperty(
      name = "Spine base joint",
      description = "Bone where spine base joint is mapped or empty if not mapped")

   spine_mid_joint = bpy.props.StringProperty(
      name = "Spine mid joint",
      description = "Bone where spine mid joint is mapped or empty if not mapped")

   neck_joint = bpy.props.StringProperty(
      name = "Neck joint",
      description = "Bone where neck joint is mapped or empty if not mapped")

   head_joint = bpy.props.StringProperty(
      name = "Head joint",
      description = "Bone where head joint is mapped or empty if not mapped")

   shoulder_left_joint = bpy.props.StringProperty(
      name = "Shoulder left joint",
      description = "Bone where shoulder left joint is mapped or empty if not mapped")

   elbow_left_joint = bpy.props.StringProperty(
      name = "Elbow left joint",
      description = "Bone where elbow left joint is mapped or empty if not mapped")

   wrist_left_joint = bpy.props.StringProperty(
      name = "Wrist left joint",
      description = "Bone where wrist left joint is mapped or empty if not mapped")

   hand_left_joint = bpy.props.StringProperty(
      name = "Hand left joint",
      description = "Bone where hand left joint is mapped or empty if not mapped")

   shoulder_right_joint = bpy.props.StringProperty(
      name = "Shoulder right joint",
      description = "Bone where shoulder right joint is mapped or empty if not mapped")

   elbow_right_joint = bpy.props.StringProperty(
      name = "Spine base joint",
      description = "Bone where elbow right joint is mapped or empty if not mapped")

   wrist_right_joint = bpy.props.StringProperty(
      name = "Wrist right joint",
      description = "Bone where wrist right joint is mapped or empty if not mapped")

   hand_right_joint = bpy.props.StringProperty(
      name = "Hand right joint",
      description = "Bone where hand right joint is mapped or empty if not mapped")

   hip_left_joint = bpy.props.StringProperty(
      name = "Hip left joint",
      description = "Bone where hip left joint is mapped or empty if not mapped")

   knee_left = bpy.props.StringProperty(
      name = "Knee left joint",
      description = "Bone where knee left joint is mapped or empty if not mapped")

   ankle_left_joint = bpy.props.StringProperty(
      name = "Ankle left joint",
      description = "Bone where ankle left joint is mapped or empty if not mapped")

   foot_left_joint = bpy.props.StringProperty(
      name = "Foot left joint",
      description = "Bone where foot left joint is mapped or empty if not mapped")

   hip_right_joint = bpy.props.StringProperty(
      name = "Hip right joint",
      description = "Bone where hip right joint is mapped or empty if not mapped")

   knee_right_joint = bpy.props.StringProperty(
      name = "Knee right joint",
      description = "Bone where knee right joint is mapped or empty if not mapped")

   ankle_right_joint = bpy.props.StringProperty(
      name = "Ankle right joint",
      description = "Bone where ankle right joint is mapped or empty if not mapped")

   foot_right_joint = bpy.props.StringProperty(
      name = "Foot right joint",
      description = "Bone where foot right joint is mapped or empty if not mapped")

   spine_shoulder_joint = bpy.props.StringProperty(
      name = "Spine shoulder joint",
      description = "Bone where spine shoulder joint is mapped or empty if not mapped")

   hand_tip_left_joint = bpy.props.StringProperty(
      name = "Hand tip left joint",
      description = "Bone where hand tip left joint is mapped or empty if not mapped")

   thumb_left_joint = bpy.props.StringProperty(
      name = "Thumb left joint",
      description = "Bone where thumb left joint is mapped or empty if not mapped")

   hand_tip_right_joint = bpy.props.StringProperty(
      name = "Hand tip right joint",
      description = "Bone where hand tip right joint is mapped or empty if not mapped")

   thumb_right_joint = bpy.props.StringProperty(
      name = "Thumb right joint",
      description = "Bone where thumb right joint is mapped or empty if not mapped")

def register():
   bpy.utils.register_module(__name__)

   bpy.types.WindowManager.kinect_motion = bpy.props.PointerProperty(type = KinectMotionWindowManagerPropertyGroup)
   bpy.types.Scene.kinect_motion = bpy.props.PointerProperty(type = KinectMotionScenePropertyGroup)

   # Initially, set motion capture off.
   bpy.context.window_manager.kinect_motion.toggle_capture = False

def unregister():
   del bpy.types.Scene.kinect_motion
   del bpy.types.WindowManager.kinect_motion

   bpy.utils.unregister_module(__name__)
