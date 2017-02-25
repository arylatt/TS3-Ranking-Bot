using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Threading;
using TS3QueryLib.Net.Core.Common.Responses;
using TS3QueryLib.Net.Core.Server.Commands;
using TS3QueryLib.Net.Core.Server.Entitities;
using TS3QueryLib.Net.Core.Server.Responses;

namespace TS3_Ranking_Bot
{
    public class Client
    {
        private string _file = "[Client]    ";
        private MySqlConnection _mysql;
        private uint _clid;
        private int _dbid;
        private bool _valid = true;
        private ClientInfoCommandResponse _clinfo;
        private int _level;
        private double _time;
        private Timer _timer;
        private int _errors = 0;

        public Client(uint clid)
        {
            RankingBot.Logger.Debug(_file + " Init Client (ClientID: '" + clid + "')");
            _mysql = (MySqlConnection)RankingBot.MySQL.Clone();
            _clid = clid;
            FetchClientInfo();
            CheckExistsInDB();
            _timer = new Timer(MinPassed, null, 60000, 60000);
            RankingBot.Logger.Info(_file + " Started tracking new client (ClientID: '" + clid + "', Nickname: '" + _clinfo.Nickname + "', UniqueID: '" + _clinfo.UniqueId + "')");
            SetGroups();
        }

        private void CheckExistsInDB()
        {
            if (_clinfo.UniqueId == null || !_valid)
            {
                return;
            }

            RankingBot.Logger.Debug(_file + " Checking if client exists in DB (ClientID: '" + _clid + "')");
            ConnectDB();
            MySqlCommand c = new MySqlCommand("select * from " + RankingBot.Config.database.prefix + "users where uuid = '" + MySqlHelper.EscapeString(_clinfo.UniqueId) + "'", _mysql);
            MySqlDataReader r = c.ExecuteReader();
            if (!r.HasRows)
            {
                r.Close();
                RankingBot.Logger.Debug(_file + " Creating client in DB (ClientID: '" + _clid + "')");
                c.CommandText = "insert into " + RankingBot.Config.database.prefix + "users (uuid, cur_level, cur_time) values ('" + MySqlHelper.EscapeString(_clinfo.UniqueId) + "', 0, 0)";
                if (c.ExecuteNonQuery() != 1)
                {
                    RankingBot.Logger.Error(_file + " Could not create record in DB for client (ClientID: '" + _clid + "')");
                }
                else
                {
                    DisconnectDB();
                    CheckExistsInDB();
                }
            }
            else
            {
                RankingBot.Logger.Debug(_file + " Fetching client stats from DB (ClientID: '" + _clid + "')");
                while (r.Read())
                {
                    _dbid = (int)r["id"];
                    _level = (int)r["cur_level"];
                    _time = Convert.ToDouble(r["cur_time"]);
                }
                r.Close();
            }
            RankingBot.Logger.Debug(_file + " Finished CheckExistsInDB (ClientID: '" + _clid + "')");
            DisconnectDB();
        }

        private void FetchClientInfo()
        {
            if (!_valid)
            {
                return;
            }

            try
            {

                lock (RankingBot.TS3Lock)
                {
                    RankingBot.Logger.Debug(_file + " Fetching client info (ClientID: '" + _clid + "')");
                    ClientInfoCommandResponse clinfo = new ClientInfoCommand(_clid).Execute(RankingBot.TS3);
                    if (clinfo.IsErroneous)
                    {
                        RankingBot.Logger.Error(_file + " Received erroneous client info data (ClientID: '" + _clid + "')");
                        RankingBot.Logger.Debug(_file + " Erroneous data: " + clinfo.ResponseText);
                        Invalidate();
                        return;
                    }
                    RankingBot.Logger.Debug(_file + " Fetched client info (ClientID: '" + _clid + "')");
                    _clinfo = clinfo;
                    _errors = 0;
                }
            }
            catch (Exception e)
            {
                if (_errors != 2)
                {
                    _errors++;
                    RankingBot.Logger.Warn(_file + " Caught exception trying to get client info, will try " + (3 - _errors) + " more time(s) (ClientID: '" + _clid + "')");
                    FetchClientInfo();
                }
                else
                {
                    RankingBot.Logger.Error(_file + " Repeated exceptions (" + e.Message + ") trying to get client info. Invalidating client (ClientID: '" + _clid + "')");
                    Invalidate();
                    return;
                }
            }
        }

        private void MinPassed(object o)
        {
            FetchClientInfo();
            if (!_valid)
            {
                return;
            }

            if (RankingBot.Config.ranking.max_idle_time == null || _clinfo.IdleTime < TimeSpan.FromMinutes((double)RankingBot.Config.ranking.max_idle_time))
            {
                _time++;
                RankingBot.Logger.Debug(_file + " Client not idle, time increased to '" + _time + "' minutes (ClientID: '" + _clid + "')");
                double nextLevel = Math.Round(15 * Math.Pow(1.55, _level));
                if (_time >= nextLevel)
                {
                    _level++;
                    RankingBot.Logger.Info(_file + " Client has leveled up to level '" + _level + "' (ClientID: '" + _clid + "')");
                }

                ConnectDB();
                MySqlCommand c = new MySqlCommand("update " + RankingBot.Config.database.prefix + "users set cur_time = '" + _time + "', cur_level = '" + _level + "' where id = '" + _dbid + "'", _mysql);
                if (c.ExecuteNonQuery() != 1)
                {
                    RankingBot.Logger.Error(_file + " Error updating record in DB (ClientID: '" + _clid + "')");
                    RankingBot.Logger.Debug(_file + " Error running SQL query: '" + c.CommandText + "'");
                }
                DisconnectDB();
            }
            SetGroups();
        }

        private void SetGroups()
        {
            if (!_valid)
            {
                return;
            }

            List<uint> groupsList = new List<uint>(RankingBot.Config.ranking.ranks.ToObject<uint[]>());
            uint add = 0;

            RankingBot.Logger.Debug(_file + " Client is level " + _level + " (ClientID: '" + _clid + "')");
            if (_level - 1 >= 0)
            {
                if (groupsList.Count == 0) 
                {
                    RankingBot.Logger.Warn(_file + " There are no ranks defined in the config file.");
                }
                else if (groupsList.Count >= _level)
                {
                    add = groupsList[_level - 1];
                    RankingBot.Logger.Debug(_file + " Client should be in ServerGroup '" + add + "' (ClientID: '" + _clid + "')");
                    groupsList.RemoveAt(_level - 1);
                }
                else
                {
                    add = groupsList[groupsList.Count - 1];
                    RankingBot.Logger.Debug(_file + " Client should be in overflow ServerGroup '" + add + "' (ClientID: '" + _clid + "')");
                    groupsList.RemoveAt(groupsList.Count - 1);
                }
            }

            lock (RankingBot.TS3Lock)
            {
                new ClientUpdateCommand(new ClientModification() { Nickname = "Ranking Bot" }).Execute(RankingBot.TS3);
                if (RankingBot.Config.clients.member_group != null)
                {
                    if (!_clinfo.ServerGroups.Contains((uint)RankingBot.Config.clients.member_group))
                    {
                        RankingBot.Logger.Debug(_file + " Add client to member ServerGroup '" + RankingBot.Config.clients.member_group + "' (ClientID: '" + _clid + "')");
                        if (RankingBot.Debugging && !RankingBot.DebugRank)
                        {
                            RankingBot.Logger.Debug(_file + " Not changing server groups in debug any mode' (ClientID: '" + _clid + "')");
                        }
                        else
                        {
                            if (new ServerGroupAddClientCommand((uint)RankingBot.Config.clients.member_group, _clinfo.DatabaseId).Execute(RankingBot.TS3).IsErroneous)
                            {
                                RankingBot.Logger.Error(_file + " Unable to add client to member ServerGroup '" + RankingBot.Config.clients.member_group + "' (ClientID: '" + _clid + "')");
                            }
                        }
                    }
                }

                if (add != 0)
                {
                    if (!_clinfo.ServerGroups.Contains(add))
                    {
                        RankingBot.Logger.Debug(_file + " Add client to ServerGroup '" + add + "' (ClientID: '" + _clid + "')");
                        if (RankingBot.Debugging && !RankingBot.DebugRank)
                        {
                            RankingBot.Logger.Debug(_file + " Not changing server groups in debug any mode' (ClientID: '" + _clid + "')");
                        }
                        else
                        {
                            CommandResponse addCmd = new ServerGroupAddClientCommand(add, _clinfo.DatabaseId).Execute(RankingBot.TS3);
                            if (addCmd.IsErroneous)
                            {
                                RankingBot.Logger.Error(_file + " Unable to add client to ServerGroup '" + add + "' (ClientID: '" + _clid + "')");
                                RankingBot.Logger.Debug(_file + " Error Info: " + addCmd.ResponseText);
                            }
                        }
                    }
                }

                foreach (uint group in groupsList)
                {
                    if (_clinfo.ServerGroups.Contains(group))
                    {
                        RankingBot.Logger.Debug(_file + " Remove client from ServerGroup '" + group + "' (ClientID: '" + _clid + "')");
                        if (RankingBot.Debugging && !RankingBot.DebugRank)
                        {
                            RankingBot.Logger.Debug(_file + " Not changing server groups in debug any mode' (ClientID: '" + _clid + "')");
                        }
                        else
                        {
                            CommandResponse delCmd = new ServerGroupDelClientCommand(group, _clinfo.DatabaseId).Execute(RankingBot.TS3);
                            if (delCmd.IsErroneous)
                            {
                                RankingBot.Logger.Error(_file + " Unable to remove client from ServerGroup '" + group + "' (ClientID: '" + _clid + "')");
                                RankingBot.Logger.Debug(_file + " Error Info: " + delCmd.ResponseText);
                            }
                        }
                    }
                }
            }
        }

        private void ConnectDB()
        {
            if (_mysql.State != System.Data.ConnectionState.Open)
            {
                RankingBot.Logger.Debug(_file + " Opening a database connection");
                try
                {
                    _mysql.Open();
                }
                catch (MySqlException e)
                {
                    RankingBot.Logger.Error(_file + " Could not connect to MySQL Database: " + e.Message);
                    Invalidate();
                }
            }
        }

        private void DisconnectDB()
        {
            if (_mysql.State != System.Data.ConnectionState.Closed)
            {
                RankingBot.Logger.Debug(_file + " Closing a database connection");
                _mysql.Close();
            }
        }

        private void Invalidate()
        {
            _valid = false;
            RankingBot.Logger.Warn(_file + " Client connection has become invalid (ClientID: '" + _clid + "')");
            DisconnectDB();
            _timer.Dispose();
        }

        public void Dispose()
        {
            _valid = false;
            RankingBot.Logger.Info(_file + " Stopped tracking client (ClientID: '" + _clid + "')");
            DisconnectDB();
            _timer.Dispose();
        }
    }
}
