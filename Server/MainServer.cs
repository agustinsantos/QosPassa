using log4net;
using Mina.Core.Future;
using Mina.Core.Session;
using Mina.Filter.Codec;
using Mina.Filter.Codec.Serialization;
using Mina.Filter.Logging;
using Mina.Transport.Socket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using WhatsappQS.Message;

namespace WhatsappQS.Server
{
    class MainServer
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region Communication Login
        private IPEndPoint ListenerEndPoint;

        //private static readonly String PROCESSID = "ProcessId";

        private AsyncSocketAcceptor acceptor;
        private List<AsyncSocketConnector> servers = new List<AsyncSocketConnector>();

        private Dictionary<Guid, IoSession> clientSessions = new Dictionary<Guid, IoSession>();
        private Dictionary<Guid, IoSession> serverSessions = new Dictionary<Guid, IoSession>();
        private List<Guid> order = new List<Guid>();
        private string name;
        private bool stopOrder = true;

        private int totalServers;

        public void Init(IPEndPoint listenerEndPoint, int numServers, string name, IPEndPoint[] remoteServerAddrs = null)
        {
            this.name = name;
            if (listenerEndPoint == null)
                throw new ArgumentNullException("IPEndPoint listenerEndPoint");

            this.ListenerEndPoint = listenerEndPoint;

            acceptor = new AsyncSocketAcceptor();
            AttachClientsLogic(acceptor);

            if (remoteServerAddrs != null)
            {
                foreach (var endPoint in remoteServerAddrs)
                {
                    AsyncSocketConnector connector = new AsyncSocketConnector();
                    AttachServersLogic(connector);
                    IoSession session;
                    while (true)
                    {
                        try
                        {
                            IConnectFuture future = connector.Connect(endPoint);
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
                    servers.Add(connector);
                }
            }
            else // I'am the first server.  
            {
                order.Add(serverId);
            }
            totalServers = numServers;
        }
        private void AttachServersLogic(AsyncSocketConnector connector)
        {
            // Configure the service.
            connector.FilterChain.AddLast("codec",
                new ProtocolCodecFilter(new ObjectSerializationCodecFactory()));

            //connector.FilterChain.AddLast("logger", new LoggingFilter());

            var conf = connector.SessionConfig;

            connector.SessionOpened += (s, e) =>
            {
                ConnectMessage connectMsg = new ConnectMessage() { Guid = serverId, Role = Role.SERVER };
                e.Session.Write(connectMsg);
            };

            connector.ExceptionCaught += (s, e) =>
            {
                Console.WriteLine(e.Exception);
                e.Session.Close(true);
            };

            connector.MessageReceived += (s, e) =>
            {
                log.DebugFormat("Received message at {0}:{1}", serverId, e.Message);
                if (e.Message is ConnectMessage)
                {
                    OnConnectMessage(e);
                }
                else if (e.Message is DisconnectMessage)
                {
                    OnDisconnectMessage(e);
                }
                else if (e.Message is BroadcastPublishedMessage)
                {
                    OnBroadcastMessage(e);
                }
                else if (e.Message is BroadcastUpdatesMessage)
                {
                    OnApplyUpdates(e);
                }
                else if (e.Message is ServerOrderMessage)
                {
                    OnServerOrderMessage(e);
                }
                else
                {
                    log.DebugFormat("Received Unkown Message");
                }
            };
        }

        private void AttachClientsLogic(AsyncSocketAcceptor acceptor)
        {
            acceptor.FilterChain.AddLast("codec",
                new ProtocolCodecFilter(new ObjectSerializationCodecFactory()));

            LoggingFilter logging = new LoggingFilter();
            //logging.SessionCreatedLevel = LogLevel.None;
            //logging.SessionOpenedLevel = LogLevel.None;
            //logging.SessionClosedLevel = LogLevel.None;
            //logging.MessageSentLevel = LogLevel.None;
            //logging.MessageReceivedLevel = LogLevel.Info;
            acceptor.FilterChain.AddLast("logger", logging);

            acceptor.SessionOpened += (s, e) =>
            {
                e.Session.Config.SetIdleTime(IdleStatus.BothIdle, 10 * 60); // 10 minutes
                //e.Session.SetAttribute(PROCESSID, serverId);
            };

            acceptor.SessionIdle += (s, e) =>
            {
                // e.Session.Close(true);
            };

            acceptor.ExceptionCaught += (s, e) =>
            {
                Console.WriteLine(e.Exception);
                e.Session.Close(true);
            };

            acceptor.MessageReceived += (s, e) =>
            {
                log.DebugFormat("Received message at {0}:{1}", serverId, e.Message);

                if (e.Message is ConnectMessage)
                {
                    OnConnectMessage(e);
                }
                else if (e.Message is DisconnectMessage)
                {
                    OnDisconnectMessage(e);
                }
                else if (e.Message is PublishMessage)
                {
                    PublishMessage msg = (PublishMessage)e.Message;
                    Guid guid = msg.Guid;
                    string text = msg.MessageText;
                    log.DebugFormat("Client {0} sent Publish Message  : {1}", guid, text);
                    OnPublish(msg.MessageText);
                }
                else if (e.Message is GetFromRequestMessage)
                {
                    GetFromRequestMessage msg = (GetFromRequestMessage)e.Message;
                    Guid guid = msg.Guid;
                    int order = msg.MessageOrder;
                    log.DebugFormat("Client {0} sent GetFrom Message  : {1}", guid, order);
                    OnGetFrom(msg.MessageOrder, guid);
                }
                else if (e.Message is BroadcastPublishedMessage)
                {
                    OnBroadcastMessage(e);
                }
                else if (e.Message is BroadcastUpdatesMessage)
                {
                    OnApplyUpdates(e);
                }
                else if (e.Message is ServerOrderMessage)
                {
                    OnServerOrderMessage(e);
                }
                else
                {
                    log.DebugFormat("Received Unkown Message {0}", e.Message);
                }
            };
        }

        public void Start()
        {
            log.DebugFormat("Start Process {0}, Listening at endpoint {1}.", serverId, this.ListenerEndPoint);
            acceptor.Bind(this.ListenerEndPoint);
        }

        public void Stop()
        {
            log.DebugFormat("Stop Process {0}.", serverId);

            foreach (var session in clientSessions)
            {
                DisconnectMessage disconnectMsg = new DisconnectMessage() { Guid = serverId };
                session.Value.Write(disconnectMsg);
                session.Value.Close(false);
            }
            foreach (var session in serverSessions)
            {
                DisconnectMessage disconnectMsg = new DisconnectMessage() { Guid = serverId };
                session.Value.Write(disconnectMsg);
                session.Value.Close(false);
            }
        }

        private void OnConnectMessage(IoSessionMessageEventArgs e)
        {
            ConnectMessage msg = (ConnectMessage)e.Message;
            Guid guid = msg.Guid;
            switch (msg.Role)
            {
                case Role.CLIENT:

                    log.InfoFormat("Client {0} is Connected.", guid);
                    clientSessions.Add(guid, e.Session);
                    break;
                case Role.SERVER:
                    log.InfoFormat("Server {0} is Connected.", guid);
                    if (!serverSessions.ContainsKey(guid))
                    {
                        serverSessions.Add(guid, e.Session);
                        AddToOrder(guid);

                        ConnectMessage connectMsg = new ConnectMessage() { Guid = serverId, Role = Role.SERVER };
                        e.Session.Write(connectMsg);
                    } break;
                default:
                    log.InfoFormat("Admin {0} is Connected.", guid);
                    break;

            }
        }
        private void OnDisconnectMessage(IoSessionMessageEventArgs e)
        {
            DisconnectMessage msg = (DisconnectMessage)e.Message;
            Guid guid = msg.Guid;
            if (clientSessions.ContainsKey(guid))
                clientSessions.Remove(guid);
            if (serverSessions.ContainsKey(guid))
                serverSessions.Remove(guid);

            log.InfoFormat("Process {0} is Disconnected.", guid);
        }

        private void OnBroadcastMessage(IoSessionMessageEventArgs e)
        {
            BroadcastPublishedMessage msg = (BroadcastPublishedMessage)e.Message;
            Guid guid = msg.Guid;
            log.DebugFormat("Process {0} sent Broadcast Message.", guid);
            if (!published.Contains(guid))
            {
                published.Add(guid);
                CheckTurnTrigger();
            }
        }

        private void OnServerOrderMessage(IoSessionMessageEventArgs e)
        {
            ServerOrderMessage msg = (ServerOrderMessage)e.Message;
            Guid guid = msg.Guid;
            log.DebugFormat("Process {0} sent Order Message.", guid);
            this.order.AddRange(msg.Order);
            Turn = this.order[0];
        }

        private void OnApplyUpdates(IoSessionMessageEventArgs e)
        {
            BroadcastUpdatesMessage msg = (BroadcastUpdatesMessage)e.Message;
            OnBroadcastUpdates(msg);
        }

        public void DebugSessions()
        {
            foreach (var session in serverSessions)
            {
                log.DebugFormat("At server {0}, Session {1} to {2}", serverId, session.Value, session.Key);
            }
        }
        #endregion

        #region Server Logic

        private readonly object monitor = new object();

        private Guid serverId = Guid.NewGuid();
        private Guid turn;
        private List<string> pubs = new List<string>();
        private List<string> updates = new List<string>();
        private List<Guid> published = new List<Guid>();
        public Guid ID
        {
            get { return serverId; }
        }

        public Guid Turn
        {
            get { return turn; }
            set
            {
                if (value == Guid.Empty)
                    throw new ArgumentException("Guid can not be null nor Empty");
                turn = value;
                CheckTurnTrigger();
            }
        }



        private void OnPublish(string msg)
        {
            lock (this)
            {
                updates.Add(msg);
                // Send to all processes except itself
                AnnouncePublication();
                CheckTurnTrigger();
            }
        }

        private void OnGetFrom(int index, Guid dst)
        {
            lock (this)
            {
                if (index < 0 || index >= pubs.Count + updates.Count)
                    SendResult(dst, null, PublicationType.NONE);
                else if (index < pubs.Count)
                {
                    List<string> rst = pubs.GetRange(index, pubs.Count - index);
                    SendResult(dst, rst, PublicationType.SEQUENTIAL);
                }
                else
                {
                    List<string> rst = updates.GetRange(index - pubs.Count, updates.Count - index + pubs.Count);
                    SendResult(dst, rst, PublicationType.CAUSAL);
                }

                /*
                bool isTotalOrder = true;

                lock (monitor)
                {
                    while (updates.Count > 0 && isTotalOrder)
                        isTotalOrder = Monitor.Wait(this.monitor, this.millisecondsTimeout);
                    if (isTotalOrder)
                    {
                        log.Debug("Awakened.");
                        // return tail of pubs after publication m, flag TOTALORDER
                        int index = pubs.IndexOf(msg); // TODO mirar si es mejor buscar hacia atras, será mas eficiente
                        List<string> rst = null;
                        if (index > 0)
                            rst = pubs.GetRange(index, pubs.Count - index);
                        SendResult(dst, rst, PublicationType.TOTALORDER);
                    }
                    else
                    {
                        log.Debug("Timeout.");
                        // return tail of causalPubs after publication m, flag CAUSAL
                        int index = causalPubs.IndexOf(msg); // TODO mirar si es mejor buscar hacia atras, será mas eficiente
                        List<string> rst = null;
                        if (index > 0)
                            rst = causalPubs.GetRange(index, causalPubs.Count - index);
                        SendResult(dst, rst, PublicationType.CAUSAL);
                    }
                }
                 */
            }
        }

        /// <summary>
        /// Activated when turn_p = p
        /// </summary>
        private void ProcessMyTurn()
        {
            lock (this)
            {
                if (stopOrder)
                    return;
                pubs.AddRange(updates);
                Guid nextTurn = AuctionToken();
                SendBroadcastUpdates(nextTurn);
                updates.Clear();
                Turn = nextTurn;
            }
        }

        List<BroadcastUpdatesMessage> pendingMsg = new List<BroadcastUpdatesMessage>();

        private void OnBroadcastUpdates(BroadcastUpdatesMessage msg)
        {
            lock (this)
            {
                Guid remoteGuid = msg.Guid;

                if (turn == remoteGuid)
                {
                    ApplyUpdates(msg);

                }
                else
                    pendingMsg.Add(msg);
            }
        }

        private void ApplyUpdates(BroadcastUpdatesMessage msg)
        {
            Guid remoteGuid = msg.Guid;
            List<string> remoteUpdates = msg.Updates;
            Guid nextTurn = msg.Turn;

            pubs.AddRange(remoteUpdates);
            RemoveFromAuction(remoteGuid);
            Turn = nextTurn;
        }


        private void CheckTurnTrigger()
        {
            if (turn == serverId)
            {
                if (published.Count > 0 || updates.Count > 0)
                    ProcessMyTurn();
            }
            else
            {
                for (int i = 0; i < pendingMsg.Count; i++)
                {
                    if (pendingMsg[i].Guid == turn)
                    {
                        BroadcastUpdatesMessage msg = pendingMsg[i];
                        pendingMsg.RemoveAt(i);
                        ApplyUpdates(msg);
                    }
                }
            }
        }

        private Guid AuctionToken()
        {
            lock (this)
            {
                if (published.Count > 0)
                    return published.First();
                else
                    return serverId;
            }
        }

        private void RemoveFromAuction(Guid q)
        {
            lock (this)
            {
                published.Remove(q);
            }
        }

        private void Accounting(Guid q)
        {
            lock (this)
            {
                published.Add(q);
            }
        }

        private Guid Succesor(Guid id)
        {
            for (int i = 0; i < order.Count; i++)
            {
                if (order[i] == id)
                    if (i != order.Count - 1)
                        return order[i + 1];
                    else
                        return order[0];
            }
            return Guid.Empty;
        }

        //private Guid Succesor(Guid id, List<Guid> subset)
        //{
        //    Guid[] suborder = subset.ToArray();
        //    for (int i = 0; i < suborder.Length; i++)
        //    {
        //        if (suborder[i] == id)
        //            if (i != suborder.Length - 1)
        //                return suborder[i + 1];
        //            else
        //                return suborder[0];
        //    }
        //    return Guid.Empty;
        //}

        private void AddToOrder(Guid guid)
        {
            if (order.Count >= 1 && order[0] == serverId)
            {
                order.Add(guid);
                if (order.Count == totalServers)
                {
                    //DebugOrder();
                    ServerOrderMessage orderMsg = new ServerOrderMessage() { Guid = serverId, Order = order.ToArray() };
                    foreach (var session in serverSessions)
                    {
                        session.Value.Write(orderMsg);
                    }
                    Turn = order[0];
                }
            }
        }


        private void AnnouncePublication()
        {
            log.DebugFormat("AnnouncePublication");
            BroadcastPublishedMessage broadcastMsg = new BroadcastPublishedMessage() { Guid = serverId };
            foreach (var session in serverSessions)
            {
                session.Value.Write(broadcastMsg);
            }

        }

        public void SendBroadcastUpdates()
        {
            SendBroadcastUpdates(turn);
        }
        public void NextTurn()
        {
            if (turn == serverId)
            {
                //log.DebugFormat("It's my turn {0}", turn);
                turn = Succesor(turn);
                SendBroadcastUpdates(turn);
            }
        }

        private void SendBroadcastUpdates(Guid nextTurn)
        {
            BroadcastUpdatesMessage broadcastMsg = new BroadcastUpdatesMessage() { Guid = serverId, Turn = nextTurn, Updates = updates };
            foreach (var session in serverSessions)
            {
                session.Value.Write(broadcastMsg);
            }
        }

        private void SendResult(Guid destination, List<string> msgs, PublicationType pubType)
        {
            GetFromResultMessage msg = new GetFromResultMessage() { Guid = serverId, PubType = pubType };
            if (msgs != null)
                msg.MessageTexts = msgs.ToArray();
            GetSession(destination).Write(msg);
        }

        private IoSession GetSession(Guid dst)
        {
            return clientSessions[dst];
        }

        #endregion

        public void DebugOrder()
        {
            log.DebugFormat("At Guid {0}, turn is {1}, the order is {2}.", serverId, turn, "[" + string.Join(", ", this.order.ToList()) + "]");
        }


        public void DebugInfo()
        {
            log.DebugFormat("At Guid {0}:", serverId);
            log.DebugFormat("Stop order is {0}", this.stopOrder);
            log.DebugFormat("\tTurn is {0}, the order is {1}.", turn, "[" + string.Join(", ", this.order.ToList()) + "]");
            log.DebugFormat("\tPubs is {0}.", "[" + string.Join(", ", this.pubs.ToList()) + "]");
            log.DebugFormat("\tUpdates is {0}.", "[" + string.Join(", ", this.updates.ToList()) + "]");
        }

        public void StopOrder(bool val)
        {
            this.stopOrder = val;
        }
    }
}
