using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;

using Holo.Virtual.Users;
using System.Collections;
using Holo.Source.Managers;
using Holo.Source.GameConnectionSystem;
using Holo.Managers;

namespace Holo.GameConnectionSystem
{
    /// <summary>
    /// Asynchronous socket server for the game connections.
    /// </summary>
    public static class gameSocketServer
    {
        #region Declares
        private static Socket socketHandler;
        private static int _Port;
        private static int _maxConnections;
        private static int _acceptedConnections;
        private static HashSet<int> _activeConnections;
        //private static Hashtable connections = new Hashtable(2);
        public static bool AcceptConnections = true;
        #endregion

        #region Methods
        internal static void SetupSocket()
        {
            AcceptConnections = true;
            if (_activeConnections != null)
                _activeConnections.Clear();
            _activeConnections = new HashSet<int>();
            _Port = int.Parse(Config.getTableEntry("server_game_port"));
            _maxConnections = int.Parse(Config.getTableEntry("server_game_maxconnections"));
            Out.WriteLine("Setting up socket on port " + _Port);

            StartListening();
        }

        internal static void Destroy()
        {
            AcceptConnections = false;
            HashSet<int> Data = _activeConnections;
            if (Data == null)
                return;
            foreach (int ConnectionID in Data)
            {
                freeConnection(ConnectionID);
            }
        }

        internal static void StartListening()
        {
            try
            {
                if (_Port == 0 || _maxConnections == 0)
                    throw new NullReferenceException();
                else
                {
                    socketHandler = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    IPEndPoint Endpoint = new IPEndPoint(IPAddress.Any, _Port);
                    socketHandler.Bind(Endpoint);
                    socketHandler.Listen(1000);

                    while (!AcceptConnections)
                    {
                        socketHandler.BeginAccept(new AsyncCallback(InncommingDataRequest), socketHandler);
                    }
                }
            }
            catch (Exception e)
            {
                Out.WriteError("Fatal error during socket setup:\r" + e.ToString());
                Holo.Eucalypt.Shutdown();
            }
            finally
            {
                    socketHandler.BeginAccept(new AsyncCallback(InncommingDataRequest), socketHandler);
                    Out.WriteLine("Socket is ready");
            }
        }

        internal static void InncommingDataRequest(IAsyncResult iAr)
        {
            try
            {
                Socket ReplyFromComputer = ((Socket)iAr.AsyncState).EndAccept(iAr);
                string IP = ReplyFromComputer.RemoteEndPoint.ToString().Split(':')[0];
                //int amount = GetConnectionAmount(IP);
                if (userManager._Users.Count >= maxConnections || AcceptConnections == false)
                {
                    ReplyFromComputer.Shutdown(SocketShutdown.Both);
                    ReplyFromComputer.Close();
                    return;
                }
                else if (connectionHelper.IpIsBanned(IP) == true)
                {
                    Out.WriteRedText("Denied connection from [" + IP + "]");
                    ReplyFromComputer.Shutdown(SocketShutdown.Both);
                    ReplyFromComputer.Close();
                }
                else
                {
                    int ConnectionRequestID = NewConnectionRequestID();
                    if (ConnectionRequestID > 0)
                    {
                        Out.WriteLine("Accepted connection: [" + ConnectionRequestID + "] from [" + IP + "]");
                        

                        new gameConnection(ReplyFromComputer, ConnectionRequestID, IP);
                    }
                }
            }
            catch { }
            socketHandler.BeginAccept(new AsyncCallback(InncommingDataRequest), socketHandler);
        }

        internal static int NewConnectionRequestID()
        {
            _acceptedConnections++;
            _activeConnections.Add(_acceptedConnections);

            return _acceptedConnections;
        }

        /// <summary> 
        /// Flags a connection as free. 
        /// </summary> 
        /// <param name="connectionID">The ID of the connection.</param> 
        internal static void freeConnection(int connectionID)
        {
            if (_activeConnections.Contains(connectionID))
            {
                _activeConnections.Remove(connectionID);
                Out.WriteLine("Flagged connection [" + connectionID + "] as free.");

                _acceptedConnections--;
            }
        }

      
        #endregion

        #region Properities
        //private static int GetConnectionAmount(string IP)
        //{

        //    if (connections.ContainsKey(IP) == true)
        //    {
        //        return ((int)connections[IP]);
        //    }
        //    else
        //    {
        //        return 0;
        //    }
        //}

        internal static int maxConnections
        {
            /// <summary> 
            /// Gets or set an Integer for the maximum amount of connections at the same time. 
            /// </summary> 

            set
            {
                _maxConnections = value;
            }
            get
            {
                return _maxConnections;
            }
        }

        internal static int acceptedConnections
        {
            /// <summary> 
            /// Returns as integer of the accepted connections count since init. 
            /// </summary> 
            get { return _acceptedConnections; }
        }
        #endregion
    }
}
