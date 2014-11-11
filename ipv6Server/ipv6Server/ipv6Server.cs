
/******************************************************************************
 * THIS CODE WILL NOT COMPILE WITH .NET Framework v1.0.xxxx 
 * IT WAS BUILT USING v1.1.4322 .NET Framwork SDK
 *
 * 30.01.03 - Gary Brewer (gary@garybrewer.com)
 *****************************************************************************/
 

using System.Net.Sockets;
using System.Net;
using System;
using System.Collections;

namespace ipv6Server
{

    class Program
    {
        static void Main()
        {

            ipv6Listener v6listener = new ipv6Listener();
            v6listener.StartService();
        }

    }
}