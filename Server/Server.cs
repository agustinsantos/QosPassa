using log4net;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Reflection;
using System.Threading;
using WhatsappQS.Message;

namespace WhatsappQS.Server
{
    class Program
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static List<MainServer> servers = new List<MainServer>();

        static void Main(string[] args)
        {
            string configname = "Server1";
            foreach (var arg in args)
            {
                MainServer svr;
                configname = arg;
                string confStr = ConfigurationManager.AppSettings[configname] as string;
                string[] confParts = confStr.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                if (confParts.Length == 0)
                    throw new ApplicationException("Listener endpoint is missing");

                IPEndPoint listenAddrs = confParts[0].CreateIPEndPoint();

                IPEndPoint[] remoteAddrs = null;
                if (confParts.Length > 1)
                {
                    remoteAddrs = new IPEndPoint[confParts.Length - 1];
                    for (int cnt = 1; cnt < confParts.Length; cnt++)
                    {
                        remoteAddrs[cnt - 1] = confParts[cnt].CreateIPEndPoint();
                    }
                }

                svr = new MainServer();
                svr.Init(listenAddrs, args.Length, configname, remoteAddrs);
                svr.Start();
                servers.Add(svr);
            }

            Thread.Sleep(2000);
            DoCommands();
        }

        private static void DoCommands()
        {
            while (true)
            {
                Console.WriteLine("Enter Command:");
                string cmd = Console.ReadLine().ToLowerInvariant();
                switch (cmd)
                {
                    case "order":
                        foreach (var svr in servers)
                        {
                            svr.DebugOrder();
                        }
                        break;

                    case "dump":
                        foreach (var svr in servers)
                        {
                            svr.DebugInfo();
                        }
                        break;

                    case "startorder":
                        foreach (var svr in servers)
                        {
                            svr.StopOrder(false);
                        }
                        break;

                    case "stoporder":
                        foreach (var svr in servers)
                        {
                            svr.StopOrder(true);
                        }
                        break;

                    case "turn":
                        foreach (var svr in servers)
                        {
                            if (svr.Turn == svr.ID)
                            {
                                svr.NextTurn();
                                break;
                            }
                        }
                        break;

                    case "session":
                        foreach (var svr in servers)
                        {
                            svr.DebugSessions();
                        }
                        break;

                    case "broadcast":
                        foreach (var svr in servers)
                        {
                            svr.SendBroadcastUpdates();
                        }
                        break;

                    case "quit":
                        foreach (var svr in servers)
                            svr.Stop();
                        Console.WriteLine("Press a key to finish");
                        Console.ReadKey();
                        return;

                    case "help":
                        Console.WriteLine("help: prints this help");
                        Console.WriteLine("quit: end servers");
                        Console.WriteLine("order: prints order status");
                        Console.WriteLine("dump: prints internal status");
                        Console.WriteLine("turn: turn is update and send a broadcast status");
                        Console.WriteLine("broadcast: send a broadcast status");
                        Console.WriteLine("session: Dump debug information about sessions");
                        break;

                    default:
                        Console.WriteLine("Unknown Command {0}, type help for available commands", cmd);
                        break;
                }
            }
        }
    }
}
