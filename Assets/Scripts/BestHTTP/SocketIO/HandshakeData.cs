#if !BESTHTTP_DISABLE_SOCKETIO

using System;
using System.Collections.Generic;

namespace BestHTTP.SocketIO
{
    using BestHTTP.JSON;

    /// <summary>
    /// This class able to fill it's properties by starting a HTTP request and parsing its result. After a successfull response it will store the parsed data.
    /// </summary>
    public sealed class HandshakeData
    {
        #region Public Handshake Data

        /// <summary>
        /// Session ID of this connection.
        /// </summary>
        public string Sid { get; private set; }

        /// <summary>
        /// List of possible updgrades.
        /// </summary>
        public List<string> Upgrades { get; private set; }

        /// <summary>
        /// What interval we have to set a ping message.
        /// </summary>
        public TimeSpan PingInterval { get; private set; }

        /// <summary>
        /// What time have to pass without an answer to our ping request when we can consider the connection disconnected.
        /// </summary>
        public TimeSpan PingTimeout { get; private set; }

        #endregion

        #region Public Misc Properties

        /// <summary>
        /// The SocketManager instance that this handshake data is bound to.
        /// </summary>
        public SocketManager Manager { get; private set; }

        /// <summary>
        /// Event handler that called when the handshake data received and parsed successfully.
        /// </summary>
        public Action<HandshakeData> OnReceived;

        /// <summary>
        /// Event handler that called when an error happens.
        /// </summary>
        public Action<HandshakeData, string> OnError;

        #endregion

        private HTTPRequest HandshakeRequest;

        public HandshakeData(SocketManager manager)
        {
            this.Manager = manager;
        }

        /// <summary>
        /// Internal function, this will start a regular GET request to the server to receive the handshake data.
        /// </summary>
        internal void Start()
        {
            if (HandshakeRequest != null)
                return;

            HandshakeRequest = new HTTPRequest(new Uri(string.Format("{0}?EIO={1}&transport=polling&t={2}-{3}{4}&b64=true", 
                                                                        Manager.Uri.ToString(), 
                                                                        SocketManager.MinProtocolVersion,
                                                                        Manager.Timestamp, 
                                                                        Manager.RequestCounter++,
                                                                        Manager.Options.BuildQueryParams())),
                                               OnHandshakeCallback);

#if !BESTHTTP_DISABLE_CACHING && (!UNITY_WEBGL || UNITY_EDITOR)
            // Don't even try to cache it
            HandshakeRequest.DisableCache = true;
#endif
            HandshakeRequest.Send();

            HTTPManager.Logger.Information("HandshakeData", "Handshake request sent");
        }

        /// <summary>
        /// Internal function to be able to abort the request if necessary.
        /// </summary>
        internal void Abort()
        {
            if (HandshakeRequest != null)
                HandshakeRequest.Abort();
            HandshakeRequest = null;
            OnReceived = null;
            OnError = null;
        }

        /// <summary>
        /// Private event handler that called when the handshake request finishes.
        /// </summary>
        private void OnHandshakeCallback(HTTPRequest req, HTTPResponse resp)
        {
            HandshakeRequest = null;

            switch (req.State)
            {
                case HTTPRequestStates.Finished:
                    if (resp.IsSuccess)
                    {
                        HTTPManager.Logger.Information("HandshakeData", "Handshake data arrived: " + resp.DataAsText);

                        int idx = resp.DataAsText.IndexOf("{");
                        if (idx < 0)
                        {
                            RaiseOnError("Invalid handshake text: " + resp.DataAsText);
                            return;
                        }

                        var Handshake = Parse(resp.DataAsText.Substring(idx));

                        if (Handshake == null)
                        {
                            RaiseOnError("Parsing Handshake data failed: " + resp.DataAsText);
                            return;
                        }

                        if (OnReceived != null)
                        {
                            OnReceived(this);
                            OnReceived = null;
                        }
                    }
                    else
                        RaiseOnError(string.Format("Handshake request finished Successfully, but the server sent an error. Status Code: {0}-{1} Message: {2} Uri: {3}",
                                                                    resp.StatusCode,
                                                                    resp.Message,
                                                                    resp.DataAsText,
                                                                    req.CurrentUri));
                    break;

                case HTTPRequestStates.Error:
                    RaiseOnError(req.Exception != null ? (req.Exception.Message + " " + req.Exception.StackTrace) : string.Empty);
                    break;

                default:
                    RaiseOnError(req.State.ToString());
                    break;
            }
        }

        #region Helper Methods

        private void RaiseOnError(string err)
        {
            HTTPManager.Logger.Error("HandshakeData", "Handshake request failed with error: " + err);

            if (OnError != null)
            {
                OnError(this, err);
                OnError = null;
            }
        }

        private HandshakeData Parse(string str)
        {
            bool success = false;
            Dictionary<string, object> dict = Json.Decode(str, ref success) as Dictionary<string, object>;
            if (!success)
                return null;

            try
            {
                this.Sid = GetString(dict, "sid");
                this.Upgrades = GetStringList(dict, "upgrades");
                this.PingInterval = TimeSpan.FromMilliseconds(GetInt(dict, "pingInterval"));
                this.PingTimeout = TimeSpan.FromMilliseconds(GetInt(dict, "pingTimeout"));
            }
            catch
            {
                return null;
            }

            return this;
        }

        private static object Get(Dictionary<string, object> from, string key)
        {
            object value;
            if (!from.TryGetValue(key, out value))
                throw new System.Exception(string.Format("Can't get {0} from Handshake data!", key));
            return value;
        }

        private static string GetString(Dictionary<string, object> from, string key)
        {
            return Get(from, key) as string;
        }

        private static List<string> GetStringList(Dictionary<string, object> from, string key)
        {
            List<object> value = Get(from, key) as List<object>;

            List<string> result = new List<string>(value.Count);
            for (int i = 0; i < value.Count; ++i)
            {
                string str = value[i] as string;
                if (str != null)
                    result.Add(str);
            }

            return result;
        }

        private static int GetInt(Dictionary<string, object> from, string key)
        {
            return (int)(double)Get(from, key);
        }

        #endregion
    }
}

#endif