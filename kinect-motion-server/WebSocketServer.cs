using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KinectMotion
{
   class WebSocketServer<T> : IDisposable
   {
      struct Client
      {
         public WebSocket ws;
         public T data;
      }

      public event EventHandler<ClientConnectionChangedEventArgs<T>> ClientConnectionChanged;

      public string Protocol { get; private set; }

      private HttpListener Listener { get; set; }

      private IList<Client> Clients { get; set; }

      public WebSocketServer(int port, string protocol)
      {
         Listener = new HttpListener();
         Clients = new List<Client>();
         Protocol = protocol;

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

      public async Task SendAsync(ArraySegment<byte> bytes, Func<T, bool> predicate = null)
      {
         var tasks = new List<Task>();

         lock (Clients)
         {

            foreach (var client in Clients)
            {

               // Send bytes to clients that match the predicate, or all if the predicate is null.
               if (predicate == null || predicate(client.data))
                  tasks.Add(
                     client.ws.SendAsync(bytes, WebSocketMessageType.Binary, true, CancellationToken.None)

                     // Ignore task result completely. This will allow some send requests
                     // to throw an exception if for example the socket has died.
                     .ContinueWith(_ => Task.FromResult<object>(null)));
            }
         }

         await Task.WhenAll(tasks);
      }

      private async Task<ArraySegment<byte>> ReceiveAsync(WebSocket ws, ArraySegment<byte> buffer, CancellationToken cancellationToken) {

         // Read data from WebSocket to buffer.
         var result = await ws.ReceiveAsync(buffer, cancellationToken);

         // Stop when the message has been completely received and set array segment to delimite the
         // full array.
         if (result.EndOfMessage)
            return new ArraySegment<byte>(buffer.Array);

         var array = buffer.Array;
         var oldLength = array.Length;

         // Otherwise, resize the array buffer since message continues.
         Array.Resize(ref array, oldLength * 2);

         // Receive rest of the message to resized array. Note that we set offset equal to old array length.
         return await ReceiveAsync(ws, new ArraySegment<byte>(array, oldLength, array.Length), cancellationToken);
      }

      public bool KeepAlive(Func<ArraySegment<byte>, T, T> handler = null)
      {
         int previousConnections;
         var removedClients = new List<T>();
         var tasks = new List<Task<Tuple<Client, ArraySegment<byte>>>>();

         lock (Clients)
         {
            previousConnections = Clients.Count;

            // Clean up all client connections.
            for (int i = Clients.Count - 1; i >= 0; --i)
            {
               var client = Clients[i];

               // Remove a connection if its state is closed, closing or aborted.
               if (client.ws.State == WebSocketState.Aborted || client.ws.State == WebSocketState.Closed || client.ws.State == WebSocketState.CloseReceived)
               {
                  removedClients.Add(Clients[i].data);
                  Clients.RemoveAt(i);
                  continue;
               }

               // Add task to receive client message.
               tasks.Add(ReceiveAsync(client.ws, new ArraySegment<byte>(new byte[1024], 0, 1024), CancellationToken.None)
                  .ContinueWith(x => x.IsCompleted && !x.IsCanceled && !x.IsFaulted
                     ? Tuple.Create(client, x.Result)
                     : Tuple.Create(client, new ArraySegment<byte>())));
            }
         }

         // Emit client connection changed event if connection count has changed.
         if (removedClients.Count > 0)
            OnClientConnectionChanged(previousConnections - removedClients.Count, previousConnections, removedClients);

         // Wait all client messages 0 millisecond to make sure that never blocks.
         Task.WaitAll(tasks.ToArray(), 0);

         if (handler != null)
         {
            foreach (var task in tasks)
            {
               var client = task.Result.Item1;

               // Handle client messages for open connections that have content and update the state.
               if (client.ws.State == WebSocketState.Open && task.Result.Item2.Count > 0)
                   client.data = handler(task.Result.Item2, client.data);
            }
         }

         // Tell if the server is still listening.
         return Listener.IsListening;
      }

      public void Dispose()
      {
         var tasks = new List<Task>();

         foreach (var client in Clients)
            tasks.Add(
               client.ws.CloseAsync(WebSocketCloseStatus.Empty, "Server is shutting down.", CancellationToken.None)

               // Ignore task result completely. This will allow some close requests
               // to throw an exception if for example the socket has died.
               .ContinueWith(_ => Task.FromResult<object>(null)));

         // Wait for all sockets to close.
         Task.WaitAll(tasks.ToArray());

         // Stop Http listener.
         Listener.Stop();
      }

      private void OnClientConnectionChanged(int currentConnections, int previousConnections, IEnumerable<T> clients)
      {
         ClientConnectionChanged?.Invoke(this,
            new ClientConnectionChangedEventArgs<T>(currentConnections, previousConnections, clients));
      }

      private async static Task<WebSocket> HandleWebSocketContext(HttpListenerContext context, string protocol)
      {
         // Accept a WebSocket using a custom transfer protocol.
         var webSocketContext = await context.AcceptWebSocketAsync(protocol);

         return webSocketContext.WebSocket;
      }

      private static void HandleHttpRequest(HttpListenerRequest request, HttpListenerResponse response)
      {
         // No regular Http requests are supported.
         throw new HttpListenerException(400, "Web socket request was expected.");
      }

      private static void HandleGetContext(IAsyncResult result)
      {
         var server = (WebSocketServer<T>)result.AsyncState;
         var context = server.Listener.EndGetContext(result);

         try
         {
            if (context.Request.IsWebSocketRequest)
            {
               var webSocket = HandleWebSocketContext(context, server.Protocol).Result;
               var client = new Client { ws = webSocket };

               int currentConnections, previousConnections;

               // Store all connected clients. These are cleaned up in KeepAlive
               // method.
               lock (server.Clients)
               {
                  previousConnections = server.Clients.Count;
                  server.Clients.Add(client);
                  currentConnections = server.Clients.Count;
               }

               server.OnClientConnectionChanged(currentConnections, previousConnections, new T[] { client.data });
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

   class ClientConnectionChangedEventArgs<T> : EventArgs
   {
      public int Connections { get; private set; }

      public int PreviousConnections { get; private set; }

      public bool Connected { get { return Connections > PreviousConnections;  } }

      public bool Disconnected { get { return Connections < PreviousConnections; } }

      public IEnumerable<T> Clients { get; private set; }

      public ClientConnectionChangedEventArgs(int currentConnections, int previousConnections, IEnumerable<T> clients)
      {
         Connections = currentConnections;
         PreviousConnections = previousConnections;
         Clients = clients;
      }
   }
}
