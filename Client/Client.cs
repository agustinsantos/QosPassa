using log4net;
using Mina.Core.Future;
using Mina.Core.Session;
using Mina.Filter.Codec;
using Mina.Filter.Codec.Serialization;
using Mina.Filter.Logging;
using Mina.Transport.Socket;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Reflection;
using System.Threading;
using WhatsappQS.Message;

namespace Client
{
    class Client
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);


        private IPEndPoint serverAddr;
        private static readonly long CONNECT_TIMEOUT = 30 * 1000L; // 30 seconds

        public Guid clientId = Guid.NewGuid();
        private IoSession session;
        private AsyncSocketConnector connector;
        private static int cnt = 0;

        public void Init(IPEndPoint remoteServerAddr = null)
        {
            serverAddr = remoteServerAddr;
            connector = new AsyncSocketConnector();

            // Configure the service.
            connector.ConnectTimeoutInMillis = CONNECT_TIMEOUT;


            connector.FilterChain.AddLast("codec",
                new ProtocolCodecFilter(new ObjectSerializationCodecFactory()));


            connector.FilterChain.AddLast("logger", new LoggingFilter());

            connector.SessionOpened += (s, e) =>
            {
                ConnectMessage connectMsg = new ConnectMessage() { Guid = clientId };
                e.Session.Write(connectMsg);
            };

            connector.ExceptionCaught += (s, e) =>
            {
                Console.WriteLine(e.Exception);
                e.Session.Close(true);
            };

            connector.MessageReceived += (s, e) =>
            {
                log.Debug("Received message:" + e.Message);

                if (e.Message is DisconnectMessage)
                {
                    //OnDisconnectMessage(e);
                }
                else if (e.Message is GetFromResultMessage)
                {
                    GetFromResultMessage rm = (GetFromResultMessage)e.Message;
                    if (rm.PubType != PublicationType.NONE)
                    {
                        log.Info(" Message has PublicationType: " + rm.PubType);
                        if (rm.MessageTexts != null)
                            // server returned OK code.
                            foreach (string text in rm.MessageTexts)
                            {
                                // print the sum and disconnect.
                                log.Info(" Message: " + text);
                            }
                        else
                            log.Info(" Message has no content");
                    }
                }
                else
                {
                    log.DebugFormat("Received Unkown Message {0}", e.Message);
                }
            };
        }
        public void Start()
        {
            log.DebugFormat("Start Process {0}, connected to endpoint {1}.", clientId, this.serverAddr);
            while (true)
            {
                try
                {
                    IConnectFuture future = connector.Connect(serverAddr);
                    future.Await();
                    session = future.Session;
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    Thread.Sleep(3000);
                }
            }
        }

        public void Publish()
        {
            PublishMessage msg = new PublishMessage() { Guid = this.clientId };
            msg.MessageText = "Message num. " + cnt;
            cnt++;
            log.DebugFormat("Client {0} sent Publish Message  : {1}", this.clientId, msg.MessageText);
            session.Write(msg);
        }

        public void GetFrom(int posStr)
        {
            GetFromRequestMessage getFromMsg = new GetFromRequestMessage() { Guid = this.clientId };
            getFromMsg.MessageOrder = posStr;
            session.Write(getFromMsg);
        }

        public void Quit()
        {
            DisconnectMessage disconnectMsg = new DisconnectMessage() { Guid = this.clientId };
            session.Write(disconnectMsg);
            session.Close(false);
            // wait until the process is done
            session.CloseFuture.Await();
        }
    }
    class Program
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static List<Client> clients = new List<Client>();

        static void Main(string[] args)
        {
            string configname = "Client1";
            foreach (var arg in args)
            {
                configname = arg;
                string confStr = ConfigurationManager.AppSettings[configname] as string;
                string[] confParts = confStr.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                if (confParts.Length == 0)
                    throw new ApplicationException("Server endpoint is missing");

                IPEndPoint serverAddr = confParts[0].CreateIPEndPoint();
                Thread.Sleep(1000);

                Client client = new Client();
                client.Init(serverAddr);
                client.Start();
                clients.Add(client);
            }

            Thread.Sleep(2000);
            DoCommands();
        }
        private static void OnDisconnectMessage(IoSessionMessageEventArgs e)
        {
            DisconnectMessage msg = (DisconnectMessage)e.Message;
            Guid guid = msg.Guid;
            log.InfoFormat("Process {0} is Disconnected.", guid);
            Console.WriteLine("Press a key to finish");
            Console.ReadKey();
            Environment.Exit(1);
        }

        private static void DoCommands()
        {
            int activeClient = 0;

            while (true)
            {
                Console.WriteLine("Enter Command:");
                string cmd = Console.ReadLine().ToLowerInvariant();
                switch (cmd)
                {
                    case "publish":
                        clients[activeClient].Publish();
                        break;

                    case "getfrom":
                        Console.WriteLine("From which position?");
                        string posStr = Console.ReadLine();

                        clients[activeClient].GetFrom(int.Parse(posStr));
                        break;

                    case "next":
                        activeClient++;
                        if (activeClient == clients.Count)
                            activeClient = 0;

                        Console.WriteLine("Next client set to {0}", activeClient);
                        break;

                    case "quit":
                        foreach (var client in clients)
                            client.Quit();

                        Console.WriteLine("Press a key to finish");
                        Console.ReadKey();
                        return;

                    case "help":
                        Console.WriteLine("publish: send a msg to the system");
                        Console.WriteLine("getfrom: get msgs from a position");
                        Console.WriteLine("next: set next client as active");
                        Console.WriteLine("help: prints this help");
                        Console.WriteLine("quit: end servers");
                        break;

                    default:
                        Console.WriteLine("Unknown Command {0}, type help for available commands", cmd);
                        break;
                }
            }
        }
    }
}
