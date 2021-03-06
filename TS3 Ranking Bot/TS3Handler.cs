﻿using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using TS3QueryLib.Net.Core.Common;
using TS3QueryLib.Net.Core.Common.Responses;
using TS3QueryLib.Net.Core.Server.Commands;
using TS3QueryLib.Net.Core.Server.Entitities;
using TS3QueryLib.Net.Core.Server.Notification;

namespace TS3_Ranking_Bot
{
    public class TS3Handler
    {
        private string _file = "[TS3Handler]";
        private Timer _keepAlive;
        private bool _running = true;
        private bool _recon = false;
        private Dictionary<uint, Client> _clients;

        public TS3Handler(bool recon = false)
        {
            _recon = recon;

            RankingBot.Logger.Debug(_file + " Init TS3 Handler");
            NotificationHub notify = new NotificationHub();
            notify.ClientJoined.Triggered += TS3_ClientJoined;
            notify.ClientLeft.Banned += TS3_ClientLeft_Banned;
            notify.ClientLeft.ConnectionLost += TS3_ClientLeft_ConnectionLost;
            notify.ClientLeft.Disconnected += TS3_ClientLeft_Disconnected;
            notify.ClientLeft.Kicked += TS3_ClientLeft_Kicked;

            RankingBot.ConnectTS3((string)RankingBot.Config.teamspeak.sq_ip, (ushort)RankingBot.Config.teamspeak.sq_port, notify);
            RankingBot.TS3.BanDetected += TS3_BanDetected;
            RankingBot.TS3.ConnectionClosed += TS3_ConnectionClosed;

            try
            {
                RankingBot.TS3.Connect();
                RankingBot.Logger.Debug(_file + " Connected to TS3 server");
            }
            catch (SocketException e)
            {
                RankingBot.Logger.Error(_file + " Error connecting to TS3 server: " + e.Message);
                if (!_recon)
                {
                    Environment.Exit(1);
                }
                RankingBot.TryReconnect();
                return;
            }

            RankingBot.Logger.Debug(_file + " Logging in to ServerQuery");
            if (new LoginCommand((string)RankingBot.Config.teamspeak.username, (string)RankingBot.Config.teamspeak.password).Execute(RankingBot.TS3).IsErroneous)
            {
                RankingBot.Logger.Error(_file + " ServerQuery username/password invalid");
                Environment.Exit(1);
            }

            RankingBot.Logger.Debug(_file + " Selecting virtual server");
            if (new UseCommand((ushort)RankingBot.Config.teamspeak.ts_port).Execute(RankingBot.TS3).IsErroneous)
            {
                RankingBot.Logger.Error(_file + " Unable to access TS3 server running on port '" + RankingBot.Config.teamspeak.ts_port + "'");
                if (!_recon)
                {
                    Environment.Exit(1);
                }
                RankingBot.TryReconnect();
                return;
            }

            _clients = new Dictionary<uint, Client>();
            RankingBot.Logger.Debug(_file + " Registering for notifications");
            if (new ServerNotifyRegisterCommand(ServerNotifyRegisterEvent.Server).Execute(RankingBot.TS3).IsErroneous)
            {
                RankingBot.Logger.Error(_file + " Unable to register for server notifications");
                Environment.Exit(1);
            }

            RankingBot.Logger.Info(_file + " Connected to TS3");
            _keepAlive = new Timer(TS3_KeepAlive, null, 30000, 30000);
            RegisterConnectedClients();
            Console.CancelKeyPress += Console_CancelKeyPress;
            
            new UpdateChecker();

            while (_running)
            {
                MenuInput(Console.ReadLine());
            }
        }

        private void RegisterConnectedClients()
        {
            RankingBot.Logger.Debug(_file + " Registering connected clients");
            EntityListCommandResponse<ClientListEntry> clients =  new ClientListCommand(true).Execute(RankingBot.TS3);
            foreach (ClientListEntry client in clients.Values)
            {
                RankingBot.Logger.Debug(_file + " Found new client (ClientID: '" + client.ClientId + "', Nickname: '" + client.Nickname + "', UniqueID: '" + client.ClientUniqueId + "', Type: '" + client.ClientType + "')");
                if (RankingBot.Debugging && RankingBot.Debugger != null && RankingBot.Debugger != client.ClientUniqueId)
                {
                    RankingBot.Logger.Debug(_file + " Ignoring client. Debugging with a specific UniqueID (ClientID: '" + client.ClientId + "')");
                }
                else if (client.ClientType == 0)
                {
                    RankingBot.Logger.Debug(_file + " Added new client (ClientID: '" + client.ClientId + "')");
                    _clients.Add(client.ClientId, new Client(client.ClientId));
                }
            }
        }

        private void TS3_ClientJoined(object sender, TS3QueryLib.Net.Core.Server.Notification.EventArgs.ClientJoinedEventArgs e)
        {
            RankingBot.Logger.Debug(_file + " New client joined (ClientID: '" + e.ClientId + "', Nickname: '" + e.Nickname + "', UniqueID: '" + e.ClientUniqueId + "', Type: '" + e.ClientType + "')");
            if (RankingBot.Debugging && RankingBot.Debugger != null && RankingBot.Debugger != e.ClientUniqueId)
            {
                RankingBot.Logger.Debug(_file + " Ignoring client. Debugging with a specific UniqueID (ClientID: '" + e.ClientId + "')");
                return;
            }

            if (e.ClientType == 0 && _running)
            {
                RankingBot.Logger.Debug(_file + " Added new client (ClientID: '" + e.ClientId + "')");
                _clients.Add(e.ClientId, new Client(e.ClientId));
            }
        }

        private void TS3_ClientLeft(uint clid)
        {
            if (_clients[clid] != null)
            {
                _clients[clid].Dispose();
                _clients[clid] = null;
            }
        }

        private void TS3_ConnectionClosed(object sender, EventArgs<string> e)
        {
            if (!_running)
            {
                RankingBot.Logger.Info(_file + " Sucessfully shutdown");
                Environment.Exit(0);
            }
            else
            {
                RankingBot.TryReconnect();
                return;
            }
        }

        private void TS3_BanDetected(object sender, EventArgs<ICommandResponse> e)
        {
            RankingBot.Logger.Error(_file + " Ranking Bot has been banned from the TeamSpeak server");
            Environment.Exit(1);
        }

        private void TS3_ClientLeft_Kicked(object sender, TS3QueryLib.Net.Core.Server.Notification.EventArgs.ClientKickEventArgs e)
        {
            TS3_ClientLeft(e.VictimClientId);
        }

        private void TS3_ClientLeft_Disconnected(object sender, TS3QueryLib.Net.Core.Server.Notification.EventArgs.ClientDisconnectEventArgs e)
        {
            TS3_ClientLeft(e.ClientId);
        }

        private void TS3_ClientLeft_ConnectionLost(object sender, TS3QueryLib.Net.Core.Server.Notification.EventArgs.ClientConnectionLostEventArgs e)
        {
            TS3_ClientLeft(e.ClientId);
        }

        private void TS3_ClientLeft_Banned(object sender, TS3QueryLib.Net.Core.Server.Notification.EventArgs.ClientBanEventArgs e)
        {
            TS3_ClientLeft(e.VictimClientId);
        }

        private void TS3_KeepAlive(object o)
        {
            RankingBot.TS3.Send("whoami");
            RankingBot.Logger.Debug(_file + " Sent keepalive");
        }

        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            Exit();
        }

        private void Exit()
        {
            _running = false;
            _keepAlive.Dispose();
            List<uint> clients = new List<uint>(_clients.Keys);
            foreach (uint client in clients)
            {
                _clients[client].Dispose();
                _clients[client] = null;
            }
            RankingBot.TS3.Disconnect();
            Environment.Exit(0);
        }

        private void MenuInput(string cmd)
        {
            if (cmd == null)
            {
                return;
            }

            string[] cmdSplit = cmd.Split(' ');
            if (cmdSplit.Length >= 1)
            {
                switch (cmdSplit[0])
                {
                    case "gettimes":
                        int[] groups = RankingBot.Config.ranking.ranks.ToObject<int[]>();
                        for (int i = 0; i <= groups.Length - 1; i++)
                        {
                            RankingBot.Logger.Info(_file + " GETTIMES - Level " + (i + 1) + ": " + Math.Round(15 * Math.Pow(1.55, i)) + " minutes, ServerGroup '" + groups[i] + "'");
                        }
                        break;
                    case "exit":
                    case "quit":
                        RankingBot.Logger.Info(_file + " Exiting app");
                        Exit();
                        break;
                    case "show":
                        if (cmdSplit.Length == 1)
                        {
                            RankingBot.Logger.Info(_file + " SHOW - Command Usage: 'show users' OR 'show user <ClientID>'");
                        }
                        else
                        {
                            if (cmdSplit[1] == "users")
                            {
                                List<uint> clients = new List<uint>(_clients.Keys);
                                foreach (uint client in clients)
                                {
                                    RankingBot.Logger.Info(_file + " SHOW USERS - " + _clients[client].GetInfo());
                                }
                            }
                            else if (cmdSplit[1] == "user")
                            {
                                if (cmdSplit.Length == 2)
                                {
                                    RankingBot.Logger.Info(_file + " SHOW USER - Command Usage: 'show user <ClientID>'");
                                }
                                else
                                {
                                    uint c;
                                    if (UInt32.TryParse(cmdSplit[2], out c))
                                    {
                                        Client cli;
                                        if (!_clients.TryGetValue(c, out cli))
                                        {
                                            RankingBot.Logger.Info(_file + " SHOW USER " + cmdSplit[2] + ": ClientID not found");
                                        }
                                        else
                                        {
                                            RankingBot.Logger.Info(_file + " SHOW USER - " + cli.GetInfo());
                                        }
                                    }
                                    else
                                    {
                                        RankingBot.Logger.Info(_file + " SHOW USER " + cmdSplit[2] + ": ClientID not found");
                                    }
                                }
                            }
                        }
                        break;
                    case "set":
                        if (cmdSplit.Length != 4)
                        {
                            RankingBot.Logger.Info(_file + " SET - Command Usage: 'set level <ClientID> <Level>'");
                        }
                        else
                        {
                            if (cmdSplit[1] == "level")
                            {
                                uint c;
                                if (UInt32.TryParse(cmdSplit[2], out c))
                                {
                                    Client cli;
                                    if (!_clients.TryGetValue(c, out cli))
                                    {
                                        RankingBot.Logger.Info(_file + " SET LEVEL " + cmdSplit[2] + ": ClientID not found");
                                    }
                                    else
                                    {
                                        int tl;
                                        if (Int32.TryParse(cmdSplit[3], out tl))
                                        {
                                            cli.SetLevel(tl);
                                        }
                                    }
                                }
                                else
                                {
                                    RankingBot.Logger.Info(_file + " SET LEVEL " + ": ClientID not found");
                                }
                            }
                            else
                            {
                                RankingBot.Logger.Info(_file + " SET - Command Usage: 'set level <ClientID> <Level>'");
                            }
                        }
                        break;
                    default:
                        RankingBot.Logger.Warn(_file + " Unknown console command '" + cmdSplit[0] + "'");
                        break;
                }
            }
        }
    }
}
