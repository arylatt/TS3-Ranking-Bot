using System;
using System.Collections.Generic;
using System.Net.Sockets;
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
    public class TS3Handler
    {
        private string _file = "[TS3Handler]";
        private Timer _keepAlive;

        public TS3Handler()
        {
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
                Environment.Exit(1);
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
                Environment.Exit(1);
            }

            RankingBot.Logger.Debug(_file + " Registering for notifications");
            if (new ServerNotifyRegisterCommand(ServerNotifyRegisterEvent.Server).Execute(RankingBot.TS3).IsErroneous)
            {
                RankingBot.Logger.Error(_file + " Unable to register for server notifications");
                Environment.Exit(1);
            }

            RankingBot.Logger.Info(_file + " Connected to TS3");
            _keepAlive = new Timer(TS3_KeepAlive, null, 30000, 30000);
        }

        private void TS3_ClientJoined(object sender, TS3QueryLib.Net.Core.Server.Notification.EventArgs.ClientJoinedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void TS3_ClientLeft(uint clid)
        {
            throw new NotImplementedException();
        }

        private void TS3_ConnectionClosed(object sender, EventArgs<string> e)
        {
            throw new NotImplementedException();
        }

        private void TS3_BanDetected(object sender, EventArgs<ICommandResponse> e)
        {
            throw new NotImplementedException();
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
    }
}
