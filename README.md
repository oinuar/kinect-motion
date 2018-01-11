KinectMotion
====

This repository contains tools to make Kinect devices to stream their data in
JSON format through WebSocket. It gives you full power to utilize Kinect device
and integrate it easily to any program. See examples directory for example 
integrations.


Requirements
====

`kinect-motion-server` will work only in Windows. This is because I explorered 
different integrations and many them were buggy and prone to noise. Microsoft's 
Kinect SDK interface is a clear winner since it is fast and can output very good
quality frames. 

- Kinect V2 with Microsoft's SDK. You can download SDK here https://www.microsoft.com/en-us/download/details.aspx?id=44561.
- Windows 8.1 or later.
- Ability to connect and power Kinect from USB port. There are official ways and mods that you can do yourself.


How to get it running?
====

0. Connect Kinect V2 device to computer.
1. Compile `kinect-motion-server`.
2. Start `kinect-motion-server`.
3. Open `examples/complex.html` in a web browser that supports WebSockets.
4. Build something awesome.
