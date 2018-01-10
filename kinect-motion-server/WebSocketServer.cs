using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KinectMotion
{
   class WebSocketServer : IDisposable
   {
      public event EventHandler<ClientConnectionEventArgs> ClientConnected;

      private HttpListener Listener { get; set; }

      private IList<WebSocket> WebSockets { get; set; }

      public const string Protocol = "KinectV2MotionV1";

      public WebSocketServer(int port)
      {
         Listener = new HttpListener();
         WebSockets = new List<WebSocket>();

         // Bind only to one endpoint.
         Listener.Prefixes.Add(string.Format("http://localhost:{0}/", port));
      }

      public void Start()
      {
         // Start listener and accept Http contexts.
         Listener.Start();
         Listener.BeginGetContext(new AsyncCallback(HandleGetContext), this);
      }

      public IEnumerable<string> Endpoints
      {
         get
         {
            return Listener.Prefixes;
         }
      }

      public async Task Send(byte[] bytes)
      {
         var tasks = new List<Task>();
         var segment = new ArraySegment<byte>(bytes);

         lock (WebSockets)
         {

            // Send bytes to every client.
            foreach (var webSocket in WebSockets)
               tasks.Add(
                  webSocket.SendAsync(segment, WebSocketMessageType.Binary, true, CancellationToken.None)

                  // Ignore task result completely. This will allow some send requests
                  // to throw an exception if for example the socket has died.
                  .ContinueWith(x => Task.FromResult<object>(null)));
         }

         await Task.WhenAll(tasks);
      }

      public bool KeepAlive()
      {
         lock (WebSockets)
         {

            // Clean up all client connections.
            for (int i = WebSockets.Count - 1; i >= 0; --i)
            {

               // Remove a connection if its state is closed, closing or aborted.
               if (WebSockets[i].State == WebSocketState.Aborted || WebSockets[i].State == WebSocketState.Closed || WebSockets[i].State == WebSocketState.CloseReceived)
                  WebSockets.RemoveAt(i);
            }
         }

         // Tell if the server is still listening.
         return Listener.IsListening;
      }

      public void Dispose()
      {
         var tasks = new List<Task>();

         foreach (var webSocket in WebSockets)
            tasks.Add(
               webSocket.CloseAsync(WebSocketCloseStatus.Empty, "Server is shutting down.", CancellationToken.None)

               // Ignore task result completely. This will allow some close requests
               // to throw an exception if for example the socket has died.
               .ContinueWith(_ => Task.FromResult<object>(null)));

         // Wait for all sockets to close.
         Task.WaitAll(tasks.ToArray());

         // Stop Http listener.
         Listener.Stop();
      }

      private void OnClientConnected(int connections)
      {
         ClientConnected?.Invoke(this, new ClientConnectionEventArgs(connections));
      }

      private async static Task<WebSocket> HandleWebSocketContext(HttpListenerContext context)
      {
         // Accept a WebSocket using a custom transfer protocol.
         var webSocketContext = await context.AcceptWebSocketAsync(Protocol);

         return webSocketContext.WebSocket;
      }

      private static void HandleHttpRequest(HttpListenerRequest request, HttpListenerResponse response)
      {
         // No regular Http requests are supported.
         throw new HttpListenerException(400, "Web socket request was expected.");
      }

      private static void HandleGetContext(IAsyncResult result)
      {
         var server = (WebSocketServer)result.AsyncState;
         var context = server.Listener.EndGetContext(result);

         try
         {
            if (context.Request.IsWebSocketRequest)
            {
               var webSocket = HandleWebSocketContext(context).Result;

               int connections;

               // Store all connected clients. These are cleaned up in KeepAlive
               // method.
               lock (server.WebSockets)
               {
                  server.WebSockets.Add(webSocket);
                  connections = server.WebSockets.Count;
               }

               server.OnClientConnected(connections);
            }
            else
               HandleHttpRequest(context.Request, context.Response);
         }
         catch (HttpListenerException e)
         {
            var message = Encoding.UTF8.GetBytes(e.Message);

            context.Response.StatusCode = e.ErrorCode;
            context.Response.OutputStream.Write(message, 0, message.Length);
            context.Response.Close();
         }
         catch (Exception e)
         {
            var message = Encoding.UTF8.GetBytes(e.Message);

            context.Response.StatusCode = 500;
            context.Response.OutputStream.Write(message, 0, message.Length);
            context.Response.Close();
         }

         // Allow also other clients to connect.
         server.Listener.BeginGetContext(new AsyncCallback(HandleGetContext), server);
      }
   }

   class ClientConnectionEventArgs : EventArgs
   {
      public int Connections { get; private set; }

      public ClientConnectionEventArgs(int connections)
      {
         Connections = connections;
      }
   }
}
