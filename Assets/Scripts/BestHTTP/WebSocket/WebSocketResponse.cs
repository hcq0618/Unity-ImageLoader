#if !BESTHTTP_DISABLE_WEBSOCKET && (!UNITY_WEBGL || UNITY_EDITOR)

using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Text;

using BestHTTP.Extensions;
using BestHTTP.WebSocket.Frames;

namespace BestHTTP.WebSocket
{
    public sealed class WebSocketResponse : HTTPResponse, IHeartbeat, IProtocol
    {
        #region Public Interface

        /// <summary>
        /// Called when a Text message received
        /// </summary>
        public Action<WebSocketResponse, string> OnText;

        /// <summary>
        /// Called when a Binary message received
        /// </summary>
        public Action<WebSocketResponse, byte[]> OnBinary;

        /// <summary>
        /// Called when an incomplete frame received. No attemp will be made to reassemble these fragments.
        /// </summary>
        public Action<WebSocketResponse, WebSocketFrameReader> OnIncompleteFrame;

        /// <summary>
        /// Called when the connection closed.
        /// </summary>
        public Action<WebSocketResponse, UInt16, string> OnClosed;

        /// <summary>
        /// Indicates whether the connection to the server is closed or not.
        /// </summary>
        public bool IsClosed { get { return closed; } }

        /// <summary>
        /// On what frequency we have to send a ping to the server.
        /// </summary>
        public TimeSpan PingFrequnecy { get; private set; }

        /// <summary>
        /// Maximum size of a fragment's payload data.
        /// </summary>
        public UInt16 MaxFragmentSize { get; private set; }

        #endregion

        #region Private Fields

        private List<WebSocketFrameReader> IncompleteFrames = new List<WebSocketFrameReader>();
        private List<WebSocketFrameReader> CompletedFrames = new List<WebSocketFrameReader>();
        private WebSocketFrameReader CloseFrame;

        private object FrameLock = new object();
        private object SendLock = new object();

        /// <summary>
        /// True if we sent out a Close message to the server
        /// </summary>
        private bool closeSent;

        /// <summary>
        /// True if this WebSocket connection is closed
        /// </summary>
        private bool closed;

        private DateTime lastPing = DateTime.MinValue;

        #endregion

        internal WebSocketResponse(HTTPRequest request, Stream stream, bool isStreamed, bool isFromCache)
            : base(request, stream, isStreamed, isFromCache)
        {
            base.IsClosedManually = true;

            closed = false;
            MaxFragmentSize = UInt16.MaxValue / 2;
        }

        internal void StartReceive()
        {
            if (IsUpgraded)
            {
#if NETFX_CORE
                #pragma warning disable 4014
                    Windows.System.Threading.ThreadPool.RunAsync(ReceiveThreadFunc);
                #pragma warning restore 4014
#else
                new Thread(ReceiveThreadFunc)
                    .Start();
#endif
            }
        }

        #region Public interface for interacting with the server

        /// <summary>
        /// It will send the given message to the server in one frame.
        /// </summary>
        public void Send(string message)
        {
            if (message == null)
                throw new ArgumentNullException("message must not be null!");

            Send(new WebSocketTextFrame(message));
        }

        /// <summary>
        /// It will send the given data to the server in one frame.
        /// </summary>
        public void Send(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException("data must not be null!");

            if (data.Length > (long)MaxFragmentSize)
            {
                lock (SendLock)
                {
                    Send(new WebSocketBinaryFrame(data, 0, MaxFragmentSize, false));

                    UInt64 pos = MaxFragmentSize;
                    while (pos < (UInt64)data.Length)
                    {
                        UInt64 len = Math.Min(MaxFragmentSize, (UInt64)data.Length - pos);
                        Send(new WebSocketContinuationFrame(data, pos, len, pos + len >= (UInt64)data.Length));

                        pos += len;
                    }
                }
            }
            else
                Send(new WebSocketBinaryFrame(data));
        }

        /// <summary>
        /// Will send count bytes from a byte array, starting from offset.
        /// </summary>
        public void Send(byte[] data, ulong offset, ulong count)
        {
            if (data == null)
                throw new ArgumentNullException("data must not be null!");
            if (offset + count > (ulong)data.Length)
                throw new ArgumentOutOfRangeException("offset + count >= data.Length");

            if ((long)count > (long)MaxFragmentSize)
            {
                lock (SendLock)
                {
                    Send(new WebSocketBinaryFrame(data, offset, MaxFragmentSize, false));

                    UInt64 pos = offset + MaxFragmentSize;
                    while (pos < (UInt64)count)
                    {
                        UInt64 len = Math.Min(MaxFragmentSize, (UInt64)count - pos);
                        Send(new WebSocketContinuationFrame(data, pos, len, pos + len >= (UInt64)count));

                        pos += len;
                    }
                }
            }
            else
                Send(new WebSocketBinaryFrame(data, offset, count, true));
        }

        /// <summary>
        /// It will send the given frame to the server.
        /// </summary>
        public void Send(IWebSocketFrameWriter frame)
        {
            if (frame == null)
                throw new ArgumentNullException("frame is null!");

            if (closed)
                return;

            byte[] rawData = frame.Get();
            lock (SendLock)
            {
                Stream.Write(rawData, 0, rawData.Length);
                Stream.Flush();
            }

            if (frame.Type == WebSocketFrameTypes.ConnectionClose)
                closeSent = true;
        }

        /// <summary>
        /// It will initiate the closing of the connection to the server.
        /// </summary>
        public void Close()
        {
            Close(1000, "Bye!");
        }

        /// <summary>
        /// It will initiate the closing of the connection to the server.
        /// </summary>
        public void Close(UInt16 code, string msg)
        {
            if (closed)
                return;

            Send(new WebSocketClose(code, msg));
        }

        public void StartPinging(int frequency)
        {
            if (frequency < 100)
                throw new ArgumentException("frequency must be at least 100 millisec!");

            PingFrequnecy = TimeSpan.FromMilliseconds(frequency);

            HTTPManager.Heartbeats.Subscribe(this);
        }

        #endregion

        #region Private Threading Functions

        private void ReceiveThreadFunc(object param)
        {
            try
            {
                while (!closed)
                {
                    try
                    {
                        // TODO: reuse WebSocketFrameReader instances
                        WebSocketFrameReader frame = new WebSocketFrameReader();
                        frame.Read(Stream);

                        // A server MUST NOT mask any frames that it sends to the client.  A client MUST close a connection if it detects a masked frame.
                        // In this case, it MAY use the status code 1002 (protocol error)
                        // (These rules might be relaxed in a future specification.)
                        if (frame.HasMask)
                        {
                            Close(1002, "Protocol Error: masked frame received from server!");
                            continue;
                        }

                        if (!frame.IsFinal)
                        {
                            if (OnIncompleteFrame == null)
                                IncompleteFrames.Add(frame);
                            else
                                lock (FrameLock) CompletedFrames.Add(frame);
                            continue;
                        }

                        switch (frame.Type)
                        {
                            // For a complete documentation and rules on fragmentation see http://tools.ietf.org/html/rfc6455#section-5.4
                            // A fragmented Frame's last fragment's opcode is 0 (Continuation) and the FIN bit is set to 1.
                            case WebSocketFrameTypes.Continuation:
                                // Do an assemble pass only if OnFragment is not set. Otherwise put it in the CompletedFrames, we will handle it in the HandleEvent phase.
                                if (OnIncompleteFrame == null)
                                {
                                    frame.Assemble(IncompleteFrames);

                                    // Remove all imcomplete frames
                                    IncompleteFrames.Clear();

                                    // Control frames themselves MUST NOT be fragmented. So, its a normal text or binary frame. Go, handle it as usual.
                                    goto case WebSocketFrameTypes.Binary;
                                }
                                else
                                    lock (FrameLock) CompletedFrames.Add(frame);
                                break;

                            case WebSocketFrameTypes.Text:
                            case WebSocketFrameTypes.Binary:
                                lock (FrameLock) CompletedFrames.Add(frame);
                                break;

                            // Upon receipt of a Ping frame, an endpoint MUST send a Pong frame in response, unless it already received a Close frame.
                            case WebSocketFrameTypes.Ping:
                                if (!closeSent && !closed)
                                    Send(new WebSocketPong(frame));
                                break;

                            // If an endpoint receives a Close frame and did not previously send a Close frame, the endpoint MUST send a Close frame in response.
                            case WebSocketFrameTypes.ConnectionClose:
                                CloseFrame = frame;
                                if (!closeSent)
                                    Send(new WebSocketClose());
                                closed = closeSent;
                                break;
                        }
                    }
#if !NETFX_CORE
                    catch (ThreadAbortException)
                    {
                        IncompleteFrames.Clear();
                        this.baseRequest.State = HTTPRequestStates.Aborted;

                        closed = true;
                    }
#endif
                    catch (Exception e)
                    {
                        if (HTTPUpdateDelegator.IsCreated)
                        {
                            this.baseRequest.Exception = e;
                            this.baseRequest.State = HTTPRequestStates.Error;
                        }
                        else
                            this.baseRequest.State = HTTPRequestStates.Aborted;

                        closed = true;
                    }
                }
            }
            finally
            {
                HTTPManager.Heartbeats.Unsubscribe(this);
            }
        }

        #endregion

        #region Sending Out Events

        /// <summary>
        /// Internal function to send out received messages.
        /// </summary>
        void IProtocol.HandleEvents()
        {
            lock (FrameLock)
            {
                for (int i = 0; i < CompletedFrames.Count; ++i)
                {
                    WebSocketFrameReader frame = CompletedFrames[i];

                    // Bugs in the clients shouldn't interrupt the code, so we need to try-catch and ignore any exception occuring here
                    try
                    {
                        switch (frame.Type)
                        {
                            case WebSocketFrameTypes.Continuation:
                                if (OnIncompleteFrame != null)
                                    OnIncompleteFrame(this, frame);
                                break;

                            case WebSocketFrameTypes.Text:
                                // Any not Final frame is handled as a fragment
                                if (!frame.IsFinal)
                                    goto case WebSocketFrameTypes.Continuation;

                                if (OnText != null)
                                    OnText(this, Encoding.UTF8.GetString(frame.Data, 0, frame.Data.Length));
                                break;

                            case WebSocketFrameTypes.Binary:
                                // Any not Final frame is handled as a fragment
                                if (!frame.IsFinal)
                                    goto case WebSocketFrameTypes.Continuation;

                                if (OnBinary != null)
                                    OnBinary(this, frame.Data);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        HTTPManager.Logger.Exception("WebSocketResponse", "HandleEvents", ex);
                    }
                }

                CompletedFrames.Clear();
            }//lock (ReadLock)

            // 2015.05.09
            // State checking added becouse if there is an error the OnClose called first, and then the OnError.
            // Now, when there is an error only the OnError event will be called!
            if (IsClosed && OnClosed != null && baseRequest.State == HTTPRequestStates.Processing)
            {
                try
                {
                    UInt16 statusCode = 0;
                    string msg = string.Empty;

                    // If we received any data, we will get the status code and the message from it
                    if (CloseFrame != null && CloseFrame.Data != null && CloseFrame.Data.Length >= 2)
                    {
                        if (BitConverter.IsLittleEndian)
                            Array.Reverse(CloseFrame.Data, 0, 2);
                        statusCode = BitConverter.ToUInt16(CloseFrame.Data, 0);

                        if (CloseFrame.Data.Length > 2)
                            msg = Encoding.UTF8.GetString(CloseFrame.Data, 2, CloseFrame.Data.Length - 2);
                    }

                    OnClosed(this, statusCode, msg);
                }
                catch (Exception ex)
                {
                    HTTPManager.Logger.Exception("WebSocketResponse", "HandleEvents - OnClosed", ex);
                }
            }
        }

        #endregion

        #region IHeartbeat Implementation

        void IHeartbeat.OnHeartbeatUpdate(TimeSpan dif)
        {
            if (lastPing == DateTime.MinValue)
            {
                lastPing = DateTime.UtcNow;
                return;
            }

            if (DateTime.UtcNow - lastPing >= PingFrequnecy)
            {
                Send(new WebSocketPing(string.Empty));
                lastPing = DateTime.UtcNow;
            }
        }

        #endregion
    }
}

#endif