import bpy

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

class KinectMotionCaptureOperator(bpy.types.Operator):
    bl_idname = "kinect_motion.capture"
    bl_label = "Capture"
    bl_description = "Toggles Kinect Motion capturing"
    bl_options = { "REGISTER" }

    @classmethod
    def poll(cls, context):
        return context.window_manager.kinect_motion.toggle_capture
    
    def modal(self, context, event):
        if event.type == "ESC" or not context.window_manager.kinect_motion.toggle_capture:
            self.report({ "INFO" }, "Kinect Motion capture was cancelled")
            return { "CANCELLED" }

        print("doing stuff")
            
        return { "PASS_THROUGH" }
    
    def execute(self, context):
        return self.invoke(context, None)
    
    def invoke(self, context, event):
        context.window_manager.modal_handler_add(self)
        self.report({ "INFO" }, "Started Kinect Motion capture")
        return { "RUNNING_MODAL" }

class KinectMotionStreamPanel(bpy.types.Panel):
    bl_space_type = "VIEW_3D"
    bl_region_type = "TOOLS"
    bl_label = "Motion Stream"
    bl_category = "Kinect Motion"

    def draw(self, context):
        props = context.window_manager.kinect_motion
        prefs = context.user_preferences.addons[__name__].preferences

        row = self.layout.row()

        if props.toggle_capture:
            row.prop(props, "toggle_capture", toggle = True, text = "Stop capture", icon = "ARMATURE_DATA")
        else:
            row.prop(props, "toggle_capture", toggle = True, text = "Start capture", icon = "POSE_DATA")
            
        row = self.layout.row()
        
        row.enabled = props.toggle_capture
        
        row.prop(prefs, "auto_record_keyframes")

class KinectMotionBodyMocapPanel(bpy.types.Panel):
    bl_space_type = "VIEW_3D"
    bl_region_type = "TOOLS"
    bl_label = "Body Mocap"
    bl_category = "Kinect Motion"

    def draw(self, context):
        props = context.scene.kinect_motion
        
        # TODO: disable all of these if motion capture is toggled
        
        self.layout.row().prop_search(props, "spine_base_joint", context.scene, "objects")
        self.layout.row().prop_search(props, "spine_mid_joint", context.scene, "objects")
        self.layout.row().prop_search(props, "neck_joint", context.scene, "objects")
        self.layout.row().prop_search(props, "head_joint", context.scene, "objects")
        self.layout.row().prop_search(props, "shoulder_left_joint", context.scene, "objects")
        self.layout.row().prop_search(props, "elbow_left_joint", context.scene, "objects")
        self.layout.row().prop_search(props, "wrist_left_joint", context.scene, "objects")
        self.layout.row().prop_search(props, "hand_left_joint", context.scene, "objects")
        self.layout.row().prop_search(props, "shoulder_right_joint", context.scene, "objects")
        self.layout.row().prop_search(props, "elbow_right_joint", context.scene, "objects")
        self.layout.row().prop_search(props, "wrist_right_joint", context.scene, "objects")
        self.layout.row().prop_search(props, "hand_right_joint", context.scene, "objects")
        self.layout.row().prop_search(props, "hip_left_joint", context.scene, "objects")
        self.layout.row().prop_search(props, "knee_left", context.scene, "objects")
        self.layout.row().prop_search(props, "ankle_left_joint", context.scene, "objects")
        self.layout.row().prop_search(props, "foot_left_joint", context.scene, "objects")
        self.layout.row().prop_search(props, "hip_right_joint", context.scene, "objects")
        self.layout.row().prop_search(props, "knee_right_joint", context.scene, "objects")
        self.layout.row().prop_search(props, "ankle_right_joint", context.scene, "objects")
        self.layout.row().prop_search(props, "foot_right_joint", context.scene, "objects")
        self.layout.row().prop_search(props, "spine_shoulder_joint", context.scene, "objects")
        self.layout.row().prop_search(props, "hand_tip_left_joint", context.scene, "objects")
        self.layout.row().prop_search(props, "thumb_left_joint", context.scene, "objects")
        self.layout.row().prop_search(props, "hand_tip_right_joint", context.scene, "objects")
        self.layout.row().prop_search(props, "thumb_right_joint", context.scene, "objects")

class KinectMotionAddonPreferences(bpy.types.AddonPreferences):
    bl_idname = __name__

    endpoint = bpy.props.StringProperty(
        name = "WebSocket endpoint",
        description = "Kinect Motion WebSocket endpoint that provides the motion data",
        default = "http://localhost:8521")

    auto_record_keyframes = bpy.props.BoolProperty(
        name = "Auto record",
        description = "Automatically records keyframes when body motion is detected",
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
    spine_base_joint = bpy.props.StringProperty(
        name = "Spine base joint",
        description = "Object where spine base joint is mapped")

    spine_mid_joint = bpy.props.StringProperty(
        name = "Spine mid joint",
        description = "Object where spine mid joint is mapped")

    neck_joint = bpy.props.StringProperty(
        name = "Neck joint",
        description = "Object where neck joint is mapped")

    head_joint = bpy.props.StringProperty(
        name = "Head joint",
        description = "Object where head joint is mapped")

    shoulder_left_joint = bpy.props.StringProperty(
        name = "Shoulder left joint",
        description = "Object where shoulder left joint is mapped")

    elbow_left_joint = bpy.props.StringProperty(
        name = "Elbow left joint",
        description = "Object where elbow left joint is mapped")

    wrist_left_joint = bpy.props.StringProperty(
        name = "Wrist left joint",
        description = "Object where wrist left joint is mapped")

    hand_left_joint = bpy.props.StringProperty(
        name = "Hand left joint",
        description = "Object where hand left joint is mapped")

    shoulder_right_joint = bpy.props.StringProperty(
        name = "Shoulder right joint",
        description = "Object where shoulder right joint is mapped")

    elbow_right_joint = bpy.props.StringProperty(
        name = "Spine base joint",
        description = "Object where elbow right joint is mapped")

    wrist_right_joint = bpy.props.StringProperty(
        name = "Wrist right joint",
        description = "Object where wrist right joint is mapped")

    hand_right_joint = bpy.props.StringProperty(
        name = "Hand right joint",
        description = "Object where hand right joint is mapped")
        
    hip_left_joint = bpy.props.StringProperty(
        name = "Hip left joint",
        description = "Object where hip left joint is mapped")
        
    knee_left = bpy.props.StringProperty(
        name = "Knee left joint",
        description = "Object where knee left joint is mapped")
        
    ankle_left_joint = bpy.props.StringProperty(
        name = "Ankle left joint",
        description = "Object where ankle left joint is mapped")
        
    foot_left_joint = bpy.props.StringProperty(
        name = "Foot left joint",
        description = "Object where foot left joint is mapped")
        
    hip_right_joint = bpy.props.StringProperty(
        name = "Hip right joint",
        description = "Object where hip right joint is mapped")
        
    knee_right_joint = bpy.props.StringProperty(
        name = "Knee right joint",
        description = "Object where knee right joint is mapped")
        
    ankle_right_joint = bpy.props.StringProperty(
        name = "Ankle right joint",
        description = "Object where ankle right joint is mapped")
        
    foot_right_joint = bpy.props.StringProperty(
        name = "Foot right joint",
        description = "Object where foot right joint is mapped")
        
    spine_shoulder_joint = bpy.props.StringProperty(
        name = "Spine shoulder joint",
        description = "Object where spine shoulder joint is mapped")
        
    hand_tip_left_joint = bpy.props.StringProperty(
        name = "Hand tip left joint",
        description = "Object where hand tip left joint is mapped")
        
    thumb_left_joint = bpy.props.StringProperty(
        name = "Thumb left joint",
        description = "Object where thumb left joint is mapped")
        
    hand_tip_right_joint = bpy.props.StringProperty(
        name = "Hand tip right joint",
        description = "Object where hand tip right joint is mapped")
        
    thumb_right_joint = bpy.props.StringProperty(
        name = "Thumb right joint",
        description = "Object where thumb right joint is mapped")

def register():
    bpy.utils.register_module(__name__)
    
    bpy.types.WindowManager.kinect_motion = bpy.props.PointerProperty(type = KinectMotionWindowManagerPropertyGroup)
    bpy.types.Scene.kinect_motion = bpy.props.PointerProperty(type = KinectMotionScenePropertyGroup)
    
    # Check if capture should already be in progress and start it if so.
    if bpy.context.window_manager.kinect_motion.toggle_capture:
        bpy.ops.kinect_motion.capture("EXEC_DEFAULT")

def unregister():
    del bpy.types.Scene.kinect_motion
    del bpy.types.WindowManager.kinect_motion
    
    bpy.utils.unregister_module(__name__)
