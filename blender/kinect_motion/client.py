from json import loads
from kinect_motion.ws4py.client import WebSocketBaseClient

class Client(WebSocketBaseClient):
   def __init__(self, url):
      WebSocketBaseClient.__init__(self, url, ["KinectMotionV1"])
      self.__is_open = False
      self.__body_frame = None

   def ensure_connected(self):
      if not self.__is_open:
         self.connect()

   def bodies(self):
      if self.__body_frame == None:
         return []

      return self.__body_frame["bodies"]

   def received_message(self, message):

      # Skip all other than body frame messages.
      if message.type != "BodyFrameData":
         return

      # Parse JSON message content.
      self.__body_frame = loads(message.content)

   def opened(self):
      self.__is_open = True
