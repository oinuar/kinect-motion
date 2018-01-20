KinectMotion Blender Add-on
====

`kinect_motion` is a Blender add-on that enables you to consume Kinect motion data in Blender. This add-on requires
that you run `kinect-motion-server` which provides the data stream. With this add-on, you can transfer Kinect motion
data to Blender armatures and record bone keyframes.


How to install?
====

The add-on uses bundled PIP modules. In order to get the dependencies, you must have Python PIP installed.

1. You must install the module dependencies first:

```
pip install --target=kinect_motion 'ws4py>=0.4.3,<0.4.4'
```

2. Create a ZIP package of `kinect_motion` directory.

3. Install the ZIP add-on you created to Blender from file.

4. Add-on will be visible in Animation category and when enabled, it will create a Kinect Motion tab to 3D View.
