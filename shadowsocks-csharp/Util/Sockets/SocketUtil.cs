﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
namespace Shadowsocks.Util.Sockets
{
    public class HostInfo
    {
        private static int ipIndex = 0;
        public string HostName { get { return hostname; } }
        private static string hostname;
        private static List<IPAddress> ips;
        private static System.Timers.Timer timer;
        private static DateTime dateTime;
        public IPAddress IP
        {
            get
            {
                return GetIP(3);
            }
        }
        public bool IPAvailable
        {
            get
            {
                if (ips.Count > 0)
                {
                    return true;
                }
                return false;
            }
        }
        public bool Refresh
        {
            set
            {
                if (value == true)
                {
                    DomainResolve();
                }
            }
        }


        private IPAddress GetIP(int sec)
        {
            if (ips.Count == 1)
            {
                return ips[0];
            }
            if (ips.Count > 1)
            {
                if (ipIndex >= ips.Count)
                {
                    ipIndex = 0;
                }
                if (System.Math.Abs(DateTime.Now.Second - dateTime.Second) > sec)
                {
                    dateTime = DateTime.Now;
                    return ips[ipIndex++];
                }
                return ips[ipIndex];
            }
            return null;
        }

        public HostInfo(string host)
        {
            timer = new System.Timers.Timer();
            ips = new List<IPAddress>();
            hostname = host;
            timer.AutoReset = true;
            timer.Interval = 900000; //refresh ips,every 15 minutes
            timer.Elapsed += Timer_Elapsed;
            dateTime = DateTime.Now;
            DomainResolve();
            timer.Start();
        }
        public static bool StopTimer
        {
            set
            {
                if(value==true)
                {
                    timer?.Stop();
                }
            }  
        }
        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            timer.Stop();
            DomainResolve();
            timer.Start();
        }

        public void DeleteLastIP()
        {
            if (ips.Count > 1)
            {
                ips.RemoveAt(ipIndex);
                ipIndex++;
                if (ipIndex >= ips.Count)
                {
                    ipIndex = 0;
                }
                dateTime = DateTime.Now;
            }
        }
        private void DomainResolve()
        {
            try
            {
                ips = new List<IPAddress>();
                Shadowsocks.Controller.Logging.Info($"Resolve domain name: {hostname}");
                IPHostEntry ip = Dns.GetHostEntry(hostname);
                Ping ping = new Ping();
                if (ip != null)
                {
                    //if the domain name contain only one ip, just add it.
                    if (ip.AddressList.Length > 1)
                    {
                        foreach (IPAddress iPAddress in ip.AddressList)
                        {
                            PingReply reply = ping.Send(iPAddress, 2000);
                            if (reply.Status != IPStatus.TimedOut)
                            {
                                Shadowsocks.Controller.Logging.Info($"Find {iPAddress} for {hostname}");
                                ips.Add(iPAddress);
                            }
                        }
                    }
                    else
                    {
                        Shadowsocks.Controller.Logging.Info($"Find {ip.AddressList[0]} for {hostname}");
                        ips.Add(ip.AddressList[0]);
                    }
                }
            }
            catch
            {
                Shadowsocks.Controller.Logging.Error($"Resolve domain {hostname} failed");
            }
        }
    }
    public static class SocketUtil
    {
        private static KeyValuePair<string, HostInfo> dpPairs = new KeyValuePair<string, HostInfo>();
        public static void RefreshHostDNS(string host)
        {
            if (dpPairs.Key==host)
            {
                dpPairs.Value.Refresh = true;
            }
        }

        public static void DeleteLastIP(string host)
        {
            if (dpPairs.Key == host)
            {
                dpPairs.Value.DeleteLastIP();
            }
        }

        private class DnsEndPoint2 : DnsEndPoint
        {
            public DnsEndPoint2(string host, int port) : base(host, port)
            {
            }

            public DnsEndPoint2(string host, int port, AddressFamily addressFamily) : base(host, port, addressFamily)
            {
            }

            public override string ToString()
            {
                return this.Host + ":" + this.Port;
            }
        }

        public static EndPoint GetServerEndPoint(string host, int port)
        {
            IPAddress ipAddress;
            HostInfo hostInfo;
            bool parsed = IPAddress.TryParse(host, out ipAddress);
            if (parsed)
            {
                hostInfo = null;
                dpPairs = new KeyValuePair<string, HostInfo>();
                return new IPEndPoint(ipAddress, port);
            }
            // maybe is a domain name
            if (dpPairs.Key == host)
            {
                if (dpPairs.Value.IPAvailable)
                    return new IPEndPoint(dpPairs.Value.IP, port);
            }
            else
            {
                hostInfo= new HostInfo(host);
                dpPairs=new KeyValuePair<string, HostInfo>(host, hostInfo);
                if (dpPairs.Value.IPAvailable)
                    return new IPEndPoint(dpPairs.Value.IP, port);
            }
            // maybe is a domain name
            return new DnsEndPoint2(host, port);
        }

        public static EndPoint GetEndPoint(string host, int port)
        {
            IPAddress ipAddress;
            bool parsed = IPAddress.TryParse(host, out ipAddress);
            if (parsed)
            {
                return new IPEndPoint(ipAddress, port);
            }
            // maybe is a domain name
            return new DnsEndPoint2(host, port);
        }


        public static void FullClose(this System.Net.Sockets.Socket s)
        {
            try
            {
                s.Shutdown(SocketShutdown.Both);
            }
            catch (Exception)
            {
            }
            try
            {
                s.Disconnect(false);
            }
            catch (Exception)
            {
            }
            try
            {
                s.Close();
            }
            catch (Exception)
            {
            }
        }

    }
}
