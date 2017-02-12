using System;
using System.Collections.Generic;
using System.Threading;
using TS3QueryLib.Net.Core;
using TS3QueryLib.Net.Core.Common;
using TS3QueryLib.Net.Core.Common.Responses;
using TS3QueryLib.Net.Core.Server.Commands;
using TS3QueryLib.Net.Core.Server.Entitities;
using TS3QueryLib.Net.Core.Server.Notification;
using TS3QueryLib.Net.Core.Server.Responses;

namespace TS3_Ranking_Bot
{
    class TS3Handler
    {
        private dynamic _config;
        private Timer _keepAlive;
        private bool _exiting = false;
        private Dictionary<uint, RankedClient> clients;

        public TS3Handler()
        {
            _config = RankingBot.GetConfig();

            NotificationHub notifies = new NotificationHub();
            notifies.ClientJoined.Triggered += _ts3_ClientJoined;
            notifies.ClientLeft.Banned += _ts3_ClientBanned;
            notifies.ClientLeft.ConnectionLost += _ts3_ClientConnectionLost;
            notifies.ClientLeft.Disconnected += _ts3_ClientDisconnected;
            notifies.ClientLeft.Kicked += _ts3_ClientKicked;

            RankingBot._ts3 = new QueryClient((string)_config.teamspeak.sq_ip, (ushort)_config.teamspeak.sq_port, notifies);
            RankingBot._ts3.BanDetected += _ts3_BanDetected;
            RankingBot._ts3.ConnectionClosed += _ts3_ConnectionClosed;

            try
            {
                RankingBot._ts3.Connect();
            }
            catch (System.Net.Sockets.SocketException e)
            {
                RankingBot.Log("ERROR - TS3 - " + e.Message);
                Environment.Exit(1);
            }

            if (new LoginCommand((string)_config.teamspeak.username, (string)_config.teamspeak.password).Execute(RankingBot._ts3).IsErroneous)
            {
                RankingBot.Log("ERROR - TS3 - ServerQuery Username/Password invalid", ConsoleColor.Red);
                Environment.Exit(1);
            }
            if (new UseCommand((ushort)_config.teamspeak.ts_port).Execute(RankingBot._ts3).IsErroneous)
            {
                RankingBot.Log("ERROR - TS3 - Unable to access TeamSpeak server running on port " + _config.teamspeak.ts_port, ConsoleColor.Red);
                Environment.Exit(1);
            }
            if (new ServerNotifyRegisterCommand(ServerNotifyRegisterEvent.Server).Execute(RankingBot._ts3).IsErroneous)
            {
                RankingBot.Log("ERROR - TS3 - Unable to register for server notifications", ConsoleColor.Red);
                Environment.Exit(1);
            }

            RankingBot.Log("Connected to TS3 Server", ConsoleColor.Green);
            _keepAlive = new Timer(_ts3_KeepAlive, null, 0, 30000);
            Console.CancelKeyPress += Console_CancelKeyPress;
            clients = new Dictionary<uint, RankedClient>();
            RegisterConnectedClients();

            do
            {
                string cmd = Console.ReadLine();
                switch (cmd.ToLower())
                {
                    case "exit":
                        _exiting = true;
                        RankingBot.Log("Exiting (Reason: 'exit' cmd)", ConsoleColor.Green);
                        RankingBot._ts3.Disconnect();
                        Environment.Exit(0);
                        break;
                    case "set time":
                        Console.Write("Enter the client's ID: ");
                        try
                        {
                            uint clid = Convert.ToUInt32(Console.ReadLine());
                            if (clients[clid] == null)
                            {
                                Console.WriteLine("ERROR - CMD - Could not find Client ID " + clid, ConsoleColor.Red);
                                break;
                            }
                            Console.Write("Enter the new time in minutes: ");
                            double time = Convert.ToDouble(Console.ReadLine());
                            if (clients[clid] == null)
                            {
                                Console.WriteLine("ERROR - CMD - Could not find Client ID " + clid, ConsoleColor.Red);
                            }
                            clients[clid].SetTime(time);
                        }
                        catch (Exception e)
                        {
                            RankingBot.Log("ERROR - CMD - " + e.Message, ConsoleColor.Red);
                        }
                        break;
                    case "set level":
                        Console.Write("Enter the client's ID: ");
                        try
                        {
                            uint clid = Convert.ToUInt32(Console.ReadLine());
                            if (clients[clid] == null)
                            {
                                Console.WriteLine("ERROR - CMD - Could not find Client ID " + clid, ConsoleColor.Red);
                                break;
                            }
                            Console.Write("Enter the new level: ");
                            int level = Convert.ToInt32(Console.ReadLine());
                            if (clients[clid] == null)
                            {
                                Console.WriteLine("ERROR - CMD - Could not find Client ID " + clid, ConsoleColor.Red);
                            }
                            clients[clid].SetLevel(level);
                        }
                        catch (Exception e)
                        {
                            RankingBot.Log("ERROR - CMD - " + e.Message, ConsoleColor.Red);
                        }
                        break;
                    case "get times":
                        int[] ranks = _config.ranking.ranks.ToObject<int[]>();
                        if (ranks.Length == 0)
                        {
                            RankingBot.Log("WARN - CMD - No ranks defined", ConsoleColor.Yellow);
                        }
                        else
                        {
                            for (int i = 0; i <= ranks.Length - 1; i++)
                            {
                                RankingBot.Log("INFO - CMD - Level " + (i + 1) + ", ServerGroup ID " + ranks[i] + ", " + Math.Round(15 * (Math.Pow(1.55, i))) + " minutes");
                            }
                        }
                        break;
                    default:
                        RankingBot.Log("WARN - CMD - Unknown Command '" + cmd + "'", ConsoleColor.Yellow);
                        break;
                }
            }
            while (RankingBot._ts3.Connected);
            if (!_exiting)
            {
                RankingBot.TryReconnect();
            }
        }

        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            _exiting = true;
            RankingBot.Log("Exiting (Reason: Ctrl+C interrupt)", ConsoleColor.Green);
            RankingBot._ts3.Disconnect();
            Environment.Exit(0);
        }

        private void _ts3_ClientKicked(object sender, TS3QueryLib.Net.Core.Server.Notification.EventArgs.ClientKickEventArgs e)
        {
            _ts3_ClientDisconnectedAny(e.VictimClientId);
        }

        private void _ts3_ClientDisconnected(object sender, TS3QueryLib.Net.Core.Server.Notification.EventArgs.ClientDisconnectEventArgs e)
        {
            _ts3_ClientDisconnectedAny(e.ClientId);
        }

        private void _ts3_ClientConnectionLost(object sender, TS3QueryLib.Net.Core.Server.Notification.EventArgs.ClientConnectionLostEventArgs e)
        {
            _ts3_ClientDisconnectedAny(e.ClientId);
        }

        private void _ts3_ClientBanned(object sender, TS3QueryLib.Net.Core.Server.Notification.EventArgs.ClientBanEventArgs e)
        {
            _ts3_ClientDisconnectedAny(e.VictimClientId);
        }

        private void _ts3_ClientDisconnectedAny(uint clid)
        {
            if (clients[clid] != null)
            {
                clients[clid].Dispose();
                clients[clid] = null;
            }
        }

        private void _ts3_ClientJoined(object sender, TS3QueryLib.Net.Core.Server.Notification.EventArgs.ClientJoinedEventArgs e)
        {
            ClientInfoCommandResponse client = new ClientInfoCommand(e.ClientId).Execute(RankingBot._ts3);
            if (client.Type == 0)
            {
                if (RankingBot._debug && client.UniqueId != RankingBot._debugger)
                {
                    RankingBot.Log("DEBUG - TS3 - Ignoring client '" + client.Nickname + "' (" + client.UniqueId + ") as we are debugging", ConsoleColor.Magenta);
                    return;
                }
                clients.Add(e.ClientId, new RankedClient(e.ClientId));
            }
        }

        private void RegisterConnectedClients()
        {
            try
            {
                EntityListCommandResponse<ClientListEntry> clis = new ClientListCommand(true).Execute(RankingBot._ts3);
                foreach (ClientListEntry cli in clis.Values)
                {
                    if (cli.ClientType == 0)
                    {
                        if (RankingBot._debug && cli.ClientUniqueId != RankingBot._debugger)
                        {
                            RankingBot.Log("DEBUG - TS3 - Ignoring client '" + cli.Nickname + "' (" + cli.ClientUniqueId + ") as we are debugging", ConsoleColor.Magenta);
                        }
                        else
                        {
                            clients.Add(cli.ClientId, new RankedClient(cli.ClientId));
                        }
                    }
                }
            }
            catch (InvalidCastException e)
            {
                RankingBot.Log("ERROR - TS3 - " + e.Message, ConsoleColor.Red);
                Thread.Sleep(150);
                RegisterConnectedClients();
            }
        }

        private void _ts3_ConnectionClosed(object sender, EventArgs<string> e)
        {
            foreach (RankedClient cli in clients.Values)
            {
                cli.Dispose();
            }
            if (!_exiting)
            {
                RankingBot.Log("ERROR - TS3 - Connection to server lost", ConsoleColor.Red);
            }
        }

        private void _ts3_BanDetected(object sender, EventArgs<TS3QueryLib.Net.Core.Common.Responses.ICommandResponse> e)
        {
            RankingBot.Log("ERROR - TS3 - Banned from the server", ConsoleColor.Red);
            Environment.Exit(1);
        }

        private void _ts3_KeepAlive(Object o)
        {
            RankingBot._ts3.Send("whoami");
        }
    }
}
