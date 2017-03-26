#if !BESTHTTP_DISABLE_SOCKETIO
#if !BESTHTTP_DISABLE_WEBSOCKET

using System;
using System.Collections.Generic;

namespace BestHTTP.SocketIO.Transports
{
    using BestHTTP.WebSocket;
    
    /// <summary>
    /// A transport implementation that can communicate with a SocketIO server.
    /// </summary>
    internal sealed class WebSocketTransport : ITransport
    {
        public TransportStates State { get; private set; }
        public SocketManager Manager { get; private set; }
        public bool IsRequestInProgress { get; private set; }
        public WebSocket Implementation { get; private set; }

        private Packet PacketWithAttachment;
        private byte[] Buffer;

        public WebSocketTransport(SocketManager manager)
        {
            State = TransportStates.Closed;
            Manager = manager;
        }

        #region Some ITransport Implementation

        public void Open()
        {
            if (State != TransportStates.Closed)
                return;

            Uri uri = new Uri(string.Format("{0}?transport=websocket&sid={1}{2}",
                                             new UriBuilder(HTTPProtocolFactory.IsSecureProtocol(Manager.Uri) ? "wss" : "ws", 
                                                            Manager.Uri.Host, 
                                                            Manager.Uri.Port, 
                                                            Manager.Uri.PathAndQuery).Uri.ToString(),
                                             Manager.Handshake.Sid,
                                             !Manager.Options.QueryParamsOnlyForHandshake ? Manager.Options.BuildQueryParams() : string.Empty));

            Implementation = new WebSocket(uri);

            Implementation.OnOpen = OnOpen;
            Implementation.OnMessage = OnMessage;
            Implementation.OnBinary = OnBinary;
            Implementation.OnError = OnError;
            Implementation.OnClosed = OnClosed;

            Implementation.Open();

            State = TransportStates.Connecting;
        }

        /// <summary>
        /// Closes the transport and cleans up resources.
        /// </summary>
        public void Close()
        {
            if (State == TransportStates.Closed)
                return;

            State = TransportStates.Closed;

            if (Implementation != null)
                Implementation.Close();
            else
                HTTPManager.Logger.Warning("WebSocketTransport", "Close - WebSocket Implementation already null!");
            Implementation = null;
        }

        /// <summary>
        /// Polling implementation. With WebSocket it's just a skeleton.
        /// </summary>
        public void Poll()
        {
        }

        #endregion

        #region WebSocket Events

        /// <summary>
        /// WebSocket implementation OnOpen event handler.
        /// </summary>
        private void OnOpen(WebSocket ws)
        {
            if (ws != Implementation)
                return;

            HTTPManager.Logger.Information("WebSocketTransport", "OnOpen");

            State = TransportStates.Opening;

            // Send Ping Probe
            Send(new Packet(TransportEventTypes.Ping, SocketIOEventTypes.Unknown, "/", "probe"));
        }

        /// <summary>
        /// WebSocket implementation OnMessage event handler.
        /// </summary>
        private void OnMessage(WebSocket ws, string message)
        {
            if (ws != Implementation)
                return;

            if (HTTPManager.Logger.Level <= BestHTTP.Logger.Loglevels.All)
                HTTPManager.Logger.Verbose("WebSocketTransport", "OnMessage: " + message);

            try
            {
                Packet packet = new Packet(message);
                if (packet.AttachmentCount == 0)
                    OnPacket(packet);
                else
                    PacketWithAttachment = packet;
            }
            catch (Exception ex)
            {
                HTTPManager.Logger.Exception("WebSocketTransport", "OnMessage", ex);
            }
        }

        /// <summary>
        /// WebSocket implementation OnBinary event handler.
        /// </summary>
        private void OnBinary(WebSocket ws, byte[] data)
        {
            if (ws != Implementation)
                return;

            if (HTTPManager.Logger.Level <= BestHTTP.Logger.Loglevels.All)
                HTTPManager.Logger.Verbose("WebSocketTransport", "OnBinary");

            if (PacketWithAttachment != null)
            {
                PacketWithAttachment.AddAttachmentFromServer(data, false);

                if (PacketWithAttachment.HasAllAttachment)
                {
                    try
                    {
                        OnPacket(PacketWithAttachment);
                    }
                    catch (Exception ex)
                    {
                        HTTPManager.Logger.Exception("WebSocketTransport", "OnBinary", ex);
                    }
                    finally
                    {
                        PacketWithAttachment = null;
                    }
                }
            }
            else
            {
                // TODO: we received an unwanted binary message?
            }
        }

        /// <summary>
        /// WebSocket implementation OnError event handler.
        /// </summary>
        private void OnError(WebSocket ws, Exception ex)
        {
            if (ws != Implementation)
                return;

            string errorStr = string.Empty;

            if (ex != null)
                errorStr = (ex.Message + " " + ex.StackTrace);
            else
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                switch (ws.InternalRequest.State)
                {
                    // The request finished without any problem.
                    case HTTPRequestStates.Finished:
                        if (ws.InternalRequest.Response.IsSuccess || ws.InternalRequest.Response.StatusCode == 101)
                            errorStr = string.Format("Request finished. Status Code: {0} Message: {1}", ws.InternalRequest.Response.StatusCode.ToString(), ws.InternalRequest.Response.Message);
                        else
                            errorStr = string.Format("Request Finished Successfully, but the server sent an error. Status Code: {0}-{1} Message: {2}",
                                                            ws.InternalRequest.Response.StatusCode,
                                                            ws.InternalRequest.Response.Message,
                                                            ws.InternalRequest.Response.DataAsText);
                        break;

                    // The request finished with an unexpected error. The request's Exception property may contain more info about the error.
                    case HTTPRequestStates.Error:
                        errorStr = "Request Finished with Error! : " + ws.InternalRequest.Exception != null ? (ws.InternalRequest.Exception.Message + " " + ws.InternalRequest.Exception.StackTrace) : string.Empty;
                        break;

                    // The request aborted, initiated by the user.
                    case HTTPRequestStates.Aborted:
                        errorStr = "Request Aborted!";
                        break;

                    // Ceonnecting to the server is timed out.
                    case HTTPRequestStates.ConnectionTimedOut:
                        errorStr = "Connection Timed Out!";
                        break;

                    // The request didn't finished in the given time.
                    case HTTPRequestStates.TimedOut:
                        errorStr = "Processing the request Timed Out!";
                        break;
                }
#endif
            }

            HTTPManager.Logger.Error("WebSocketTransport", "OnError: " + errorStr);

            (Manager as IManager).OnTransportError(this, errorStr);
        }

        /// <summary>
        /// WebSocket implementation OnClosed event handler.
        /// </summary>
        private void OnClosed(WebSocket ws, ushort code, string message)
        {
            if (ws != Implementation)
              return;

            HTTPManager.Logger.Information("WebSocketTransport", "OnClosed");

            Close();

            (Manager as IManager).TryToReconnect();
        }

#endregion

#region Packet Sending Implementation

        /// <summary>
        /// A WebSocket implementation of the packet sending.
        /// </summary>
        public void Send(Packet packet)
        {
            if (State == TransportStates.Closed ||
                State == TransportStates.Paused)
                return;

            string encoded = packet.Encode();

            if (HTTPManager.Logger.Level <= BestHTTP.Logger.Loglevels.All)
                HTTPManager.Logger.Verbose("WebSocketTransport", "Send: " + encoded);

            if (packet.AttachmentCount != 0 || (packet.Attachments != null && packet.Attachments.Count != 0))
            {
                if (packet.Attachments == null)
                    throw new ArgumentException("packet.Attachments are null!");

                if (packet.AttachmentCount != packet.Attachments.Count)
                    throw new ArgumentException("packet.AttachmentCount != packet.Attachments.Count. Use the packet.AddAttachment function to add data to a packet!");
            }

            Implementation.Send(encoded);

            if (packet.AttachmentCount != 0)
            {
                int maxLength = packet.Attachments[0].Length + 1;
                for (int cv = 1; cv < packet.Attachments.Count; ++cv)
                    if ((packet.Attachments[cv].Length + 1) > maxLength)
                        maxLength = packet.Attachments[cv].Length + 1;

                if (Buffer == null || Buffer.Length < maxLength)
                    Array.Resize(ref Buffer, maxLength);

                for (int i = 0; i < packet.AttachmentCount; i++)
                {
                    Buffer[0] = (byte)TransportEventTypes.Message;

                    Array.Copy(packet.Attachments[i], 0, Buffer, 1, packet.Attachments[i].Length);

                    Implementation.Send(Buffer, 0, (ulong)packet.Attachments[i].Length + 1UL);
                }
            }
        }

        /// <summary>
        /// A WebSocket implementation of the packet sending.
        /// </summary>
        public void Send(List<Packet> packets)
        {
            for (int i = 0; i < packets.Count; ++i)
                Send(packets[i]);

            packets.Clear();
        }

#endregion

#region Packet Handling

        /// <summary>
        /// Will only process packets that need to upgrade. All other packets are passed to the Manager.
        /// </summary>
        private void OnPacket(Packet packet)
        {
            switch (packet.TransportEvent)
            {
                case TransportEventTypes.Message:
                case TransportEventTypes.Noop:
                    if (this.State == TransportStates.Opening)
                    {
                        // This transport are no open
                        State = TransportStates.Open;

                        // Inform our manager that we are ready to rock
                        if (!(Manager as IManager).OnTransportConnected(this))
                            return;
                    }

                    goto default;

                case TransportEventTypes.Pong:
                    // Answer for a Ping Probe. 
                    if (packet.Payload == "probe")
                    {
                        HTTPManager.Logger.Information("WebSocketTransport", "\"probe\" packet received, sending Upgrade packet");

                        // We will send an Upgrade("5") packet.
                        Send(new Packet(TransportEventTypes.Upgrade, SocketIOEventTypes.Unknown, "/", string.Empty));
                    }

                    goto default;

                default:
                    (Manager as IManager).OnPacket(packet);
                    break;
            }
        }

#endregion
    }
}

#endif
#endif