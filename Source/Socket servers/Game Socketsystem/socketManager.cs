using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Collections;

namespace Holo.Source.GameConnectionSystem
{
    public static class SocketManager
    {
        private static Hashtable SocketSys = new Hashtable(2);

        internal static void AddConnection(Socket _socket, int ConnectionID)
        {
            if (SocketSys.Contains(ConnectionID) == false)
                SocketSys.Add(_socket, ConnectionID);
        }

        internal static Socket GetSocket(int connectionID)
        {
            if (SocketSys.Contains(connectionID) == true)
            {
                Socket Return = (Socket)SocketSys[connectionID];
                return Return;
            }
            else
                return null;
        }
    }
}
