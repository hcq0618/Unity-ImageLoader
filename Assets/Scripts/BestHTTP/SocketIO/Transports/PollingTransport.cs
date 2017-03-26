#if !BESTHTTP_DISABLE_SOCKETIO

using System;

namespace BestHTTP.SocketIO.Transports
{
    internal sealed class PollingTransport : ITransport
    {
        #region Public (ITransport) Properties

        public TransportStates State { get; private set; }
        public SocketManager Manager { get; private set; }
        public bool IsRequestInProgress { get { return LastRequest != null; } }

        #endregion

        #region Private Fields

        /// <summary>
        /// The last POST request we sent to the server.
        /// </summary>
        private HTTPRequest LastRequest;

        /// <summary>
        /// Last GET request we sent to the server.
        /// </summary>
        private HTTPRequest PollRequest;

        /// <summary>
        /// The last packet with expected binary attachments
        /// </summary>
        private Packet PacketWithAttachment;

        #endregion

        public PollingTransport(SocketManager manager)
        {
            Manager = manager;
        }

        public void Open()
        {
            // First request after handshake
            var request = new HTTPRequest(new Uri(string.Format("{0}?EIO={1}&transport=polling&t={2}-{3}&sid={4}{5}&b64=true",
                                                                 Manager.Uri.ToString(),
                                                                 SocketManager.MinProtocolVersion,
                                                                 Manager.Timestamp.ToString(),
                                                                 Manager.RequestCounter++.ToString(),
                                                                 Manager.Handshake.Sid,
                                                                 !Manager.Options.QueryParamsOnlyForHandshake ? Manager.Options.BuildQueryParams() : string.Empty)),
                                          OnRequestFinished);
            
#if !BESTHTTP_DISABLE_CACHING && (!UNITY_WEBGL || UNITY_EDITOR)
            // Don't even try to cache it
            request.DisableCache = true;
#endif

            request.DisableRetry = true;

            request.Send();

            State = TransportStates.Opening;
        }

        /// <summary>
        /// Closes the transport and cleans up resources.
        /// </summary>
        public void Close()
        {
            if (State == TransportStates.Closed)
                return;

            State = TransportStates.Closed;

            /*
            if (LastRequest != null)
                LastRequest.Abort();

            if (PollRequest != null)
                PollRequest.Abort();*/
        }

        #region Packet Sending Implementation

        public void Send(Packet packet)
        {
            Send(new System.Collections.Generic.List<Packet> { packet });
        }

        public void Send(System.Collections.Generic.List<Packet> packets)
        {
            if (State != TransportStates.Open)
                throw new Exception("Transport is not in Open state!");

            if (IsRequestInProgress)
                throw new Exception("Sending packets are still in progress!");

            byte[] buffer = null;

            try
            {
                buffer = packets[0].EncodeBinary();

                for (int i = 1; i < packets.Count; ++i)
                {
                    byte[] tmpBuffer = packets[i].EncodeBinary();

                    Array.Resize(ref buffer, buffer.Length + tmpBuffer.Length);

                    Array.Copy(tmpBuffer, 0, buffer, buffer.Length - tmpBuffer.Length, tmpBuffer.Length);
                }

                packets.Clear();
            }
            catch (Exception ex)
            {
                (Manager as IManager).EmitError(SocketIOErrors.Internal, ex.Message + " " + ex.StackTrace);
                return;
            }

            LastRequest = new HTTPRequest(new Uri(string.Format("{0}?EIO={1}&transport=polling&t={2}-{3}&sid={4}{5}&b64=true",
                                                                 Manager.Uri.ToString(),
                                                                 SocketManager.MinProtocolVersion,
                                                                 Manager.Timestamp.ToString(),
                                                                 Manager.RequestCounter++.ToString(),
                                                                 Manager.Handshake.Sid,
                                                                 !Manager.Options.QueryParamsOnlyForHandshake ? Manager.Options.BuildQueryParams() : string.Empty)),
                                          HTTPMethods.Post,
                                          OnRequestFinished);


#if !BESTHTTP_DISABLE_CACHING && (!UNITY_WEBGL || UNITY_EDITOR)
            // Don't even try to cache it
            LastRequest.DisableCache = true;
#endif

            LastRequest.SetHeader("Content-Type", "application/octet-stream");
            LastRequest.RawData = buffer;
            
            LastRequest.Send();
        }

        private void OnRequestFinished(HTTPRequest req, HTTPResponse resp)
        {
            // Clear out the LastRequest variable, so we can start sending out new packets
            LastRequest = null;

            if (State == TransportStates.Closed)
                return;

            string errorString = null;

            switch (req.State)
            {
                // The request finished without any problem.
                case HTTPRequestStates.Finished:
                    if (HTTPManager.Logger.Level <= BestHTTP.Logger.Loglevels.All)
                        HTTPManager.Logger.Verbose("PollingTransport", "OnRequestFinished: " + resp.DataAsText);

                    if (resp.IsSuccess)
                        ParseResponse(resp);
                    else
                        errorString = string.Format("Polling - Request finished Successfully, but the server sent an error. Status Code: {0}-{1} Message: {2} Uri: {3}",
                                                        resp.StatusCode,
                                                        resp.Message,
                                                        resp.DataAsText,
                                                        req.CurrentUri);
                    break;

                // The request finished with an unexpected error. The request's Exception property may contain more info about the error.
                case HTTPRequestStates.Error:
                    errorString = (req.Exception != null ? (req.Exception.Message + "\n" + req.Exception.StackTrace) : "No Exception");
                    break;

                // The request aborted, initiated by the user.
                case HTTPRequestStates.Aborted:
                    errorString = string.Format("Polling - Request({0}) Aborted!", req.CurrentUri);
                    break;

                // Ceonnecting to the server is timed out.
                case HTTPRequestStates.ConnectionTimedOut:
                    errorString = string.Format("Polling - Connection Timed Out! Uri: {0}", req.CurrentUri);
                    break;

                // The request didn't finished in the given time.
                case HTTPRequestStates.TimedOut:
                    errorString = string.Format("Polling - Processing the request({0}) Timed Out!", req.CurrentUri);
                    break;
            }

            if (!string.IsNullOrEmpty(errorString))
                (Manager as IManager).OnTransportError(this, errorString);
        }

        #endregion

        #region Polling Implementation

        public void Poll()
        {
            if (PollRequest != null || State == TransportStates.Paused)
                return;

            PollRequest = new HTTPRequest(new Uri(string.Format("{0}?EIO={1}&transport=polling&t={2}-{3}&sid={4}{5}&b64=true",
                                                                Manager.Uri.ToString(),
                                                                SocketManager.MinProtocolVersion,
                                                                Manager.Timestamp.ToString(),
                                                                Manager.RequestCounter++.ToString(),
                                                                Manager.Handshake.Sid,
                                                                !Manager.Options.QueryParamsOnlyForHandshake ? Manager.Options.BuildQueryParams() : string.Empty)),
                                        HTTPMethods.Get,
                                        OnPollRequestFinished);

#if !BESTHTTP_DISABLE_CACHING && (!UNITY_WEBGL || UNITY_EDITOR)
            // Don't even try to cache it
            PollRequest.DisableCache = true;
#endif

            PollRequest.DisableRetry = true;

            PollRequest.Send();
        }

        private void OnPollRequestFinished(HTTPRequest req, HTTPResponse resp)
        {
            // Clear the PollRequest variable, so we can start a new poll.
            PollRequest = null;

            if (State == TransportStates.Closed)
                return;

            string errorString = null;

            switch (req.State)
            {
                // The request finished without any problem.
                case HTTPRequestStates.Finished:

                    if (HTTPManager.Logger.Level <= BestHTTP.Logger.Loglevels.All)
                        HTTPManager.Logger.Verbose("PollingTransport", "OnPollRequestFinished: " + resp.DataAsText);

                    if (resp.IsSuccess)
                        ParseResponse(resp);
                    else
                        errorString = string.Format("Polling - Request finished Successfully, but the server sent an error. Status Code: {0}-{1} Message: {2} Uri: {3}",
                                                            resp.StatusCode,
                                                            resp.Message,
                                                            resp.DataAsText,
                                                            req.CurrentUri);
                    break;

                // The request finished with an unexpected error. The request's Exception property may contain more info about the error.
                case HTTPRequestStates.Error:
                    errorString = req.Exception != null ? (req.Exception.Message + "\n" + req.Exception.StackTrace) : "No Exception";
                    break;

                // The request aborted, initiated by the user.
                case HTTPRequestStates.Aborted:
                    errorString = string.Format("Polling - Request({0}) Aborted!", req.CurrentUri);
                    break;

                // Ceonnecting to the server is timed out.
                case HTTPRequestStates.ConnectionTimedOut:
                    errorString = string.Format("Polling - Connection Timed Out! Uri: {0}", req.CurrentUri);
                    break;

                // The request didn't finished in the given time.
                case HTTPRequestStates.TimedOut:
                    errorString = string.Format("Polling - Processing the request({0}) Timed Out!", req.CurrentUri);
                    break;
            }

            if (!string.IsNullOrEmpty(errorString))
                (Manager as IManager).OnTransportError(this, errorString);
        }

        #endregion

        #region Packet Parsing and Handling

        /// <summary>
        /// Preprocessing and sending out packets to the manager.
        /// </summary>
        private void OnPacket(Packet packet)
        {
            if (packet.AttachmentCount != 0 && !packet.HasAllAttachment)
            {
                PacketWithAttachment = packet;
                return;
            }

            switch (packet.TransportEvent)
            {
                case TransportEventTypes.Message:
                    // We received a connect message("40"), we can change our state to open now
                    if (packet.SocketIOEvent == SocketIOEventTypes.Connect && State == TransportStates.Opening)
                    {
                        State = TransportStates.Open;

                        // Inform our manager that we are ready to rock
                        if (!(Manager as IManager).OnTransportConnected(this))
                            return;
                    }

                    goto default;

                default:
                    (Manager as IManager).OnPacket(packet);
                    break;
            }
        }

        /// <summary>
        /// Will parse the response, and send out the parsed packets.
        /// </summary>
        private void ParseResponse(HTTPResponse resp)
        {
            try
            {
                if (resp != null && resp.Data != null && resp.Data.Length >= 1)
                {
                    string msg = resp.DataAsText;

                    if (msg == "ok")
                        return;
                    
                    int idx = msg.IndexOf(':', 0);
                    int startIdx = 0;

                    while (idx >= 0 && idx < msg.Length)
                    {
                        int length = int.Parse(msg.Substring(startIdx, idx - startIdx));

                        string packetData = msg.Substring(++idx, length);

                        // Binary data?
                        if (packetData.Length > 2 && packetData[0] == 'b' && packetData[1] == '4')
                        {
                            byte[] buffer = Convert.FromBase64String(packetData.Substring(2));

                            if (PacketWithAttachment != null)
                            {
                                PacketWithAttachment.AddAttachmentFromServer(buffer, true);

                                if (PacketWithAttachment.HasAllAttachment)
                                {
                                    try
                                    {
                                        OnPacket(PacketWithAttachment);
                                    }
                                    catch(Exception ex)
                                    {
                                        HTTPManager.Logger.Exception("PollingTransport", "ParseResponse - OnPacket with attachment", ex);
                                        (Manager as IManager).EmitError(SocketIOErrors.Internal, ex.Message + " " + ex.StackTrace);
                                    }
                                    finally
                                    {
                                        PacketWithAttachment = null;
                                    }
                                }
                            }
                        }
                        else
                        {
                            try
                            {
                                Packet packet = new Packet(packetData);

                                OnPacket(packet);
                            }
                            catch(Exception ex)
                            {
                                HTTPManager.Logger.Exception("PollingTransport", "ParseResponse - OnPacket", ex);
                                (Manager as IManager).EmitError(SocketIOErrors.Internal, ex.Message + " " + ex.StackTrace);
                            }
                        }

                        startIdx = idx + length;
                        idx = msg.IndexOf(':', startIdx);
                    }
                }
            }
            catch (Exception ex)
            {
                (Manager as IManager).EmitError(SocketIOErrors.Internal, ex.Message + " " + ex.StackTrace);

                HTTPManager.Logger.Exception("PollingTransport", "ParseResponse", ex);
            }
        }

        #endregion
    }
}

#endif