from json import loads
from kinect_motion.ws4py.client import WebSocketBaseClient

class Client(WebSocketBaseClient):
   def __init__(self, url, timeout = None):
      WebSocketBaseClient.__init__(self, url, ["KinectMotionV1"])
      self.__timeout = timeout
      self.__is_open = False
      self.__body_frame = None

   def ensure_connected(self):
      if not self.__is_open:
         self.connect()

         # Set timeout once the connection is made.
         self.sock.settimeout(self.__timeout)

   def bodies(self):
      if self.__body_frame == None:
         return []

      return self.__body_frame["bodies"]

   def received_message(self, data):
      # Parse JSON message.
      message = loads(message)

      # Skip all other than body frame messages.
      if message["type"] != "BodyFrameData":
         return

      # Interpret message content as body frame.
      self.__body_frame = message["content"]

   def opened(self):
      self.__is_open = True

   def process_handshake_header(self, headers):
      no_protocol_or_extension = lambda x: not x.startswith(b'sec-websocket-protocol') and not x.startswith(b'sec-websocket-extensions')

      # Remove protocol and extension header values since ws4py cannot process them properly.
      headers = b'\r\n'.join(filter(no_protocol_or_extension, map(lambda x: x.lstrip().lower(), headers.split(b'\r\n'))))

      # Process handshake header with faked header values.
      return WebSocketBaseClient.process_handshake_header(self, headers)
