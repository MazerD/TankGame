using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;

namespace TankGame
{
    class RemotePlayer
    {
        public IPEndPoint endpoint { get; set; }
        public UdpClient client { get; set; }
        public string playerName { get; set; }
        public float leftStick { get; set; }
        public float rightStick { get; set; }
        public Tank myTank { get; set; }


        public RemotePlayer(IPAddress ip, int port, string name)
        {
            // create endpoint from IP and port
            endpoint = new IPEndPoint(ip, port);

            // create udpclient from endpoint
            client = new UdpClient(endpoint);

            // store player name
            playerName = name;
        }

        // attempts to read from the udpclient to get new control input from the remote player's client
        //  possible messages:
        //      routine input update; gamepad stick state, etc.
        //      fire control: shoot main turret, fire minigun burst
        //      specials: fire jump jets, etc.
        public bool updateInput()
        {
            // attempt to read new input data from the remote client

            return true;
        }

        // pass in list of objects to update; primarily tank state info and object positions
        public bool sendUpdates()
        {

            return true;
        }
    }
}
