KinectMotion
====

`kinect_motion` is a Blender addon that enables you to consume Kinect motion data in Blender. This addon requires
that you run `kinect-motion-server` which provides the data stream.


How to install?
====

The addon uses bundled PIP modules. In order to get the dependencies, you must have Python PIP installed.

1. You must install the module dependencies first:

```
pip install --target=kinect_motion 'ws4py>=0.4.3,<0.4.4'
```

2. Create a ZIP package of `kinect_motion` directory.

3. Install the ZIP addon you created to Blender from file.

4. Addon will be visible in Animation category and when enabled, it will create a Kinect Motion tab to 3D View.
