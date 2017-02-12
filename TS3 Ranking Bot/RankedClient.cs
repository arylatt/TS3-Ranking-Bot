using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Threading;
using TS3QueryLib.Net.Core.Server.Commands;
using TS3QueryLib.Net.Core.Server.Entitities;
using TS3QueryLib.Net.Core.Server.Responses;

namespace TS3_Ranking_Bot
{
    class RankedClient
    {
        private ClientInfoCommandResponse _clinfo;
        public uint _clid = 0;
        private MySqlConnection _mysql;
        private Timer _timer;
        private dynamic _config;
        private int _dbId = 0;
        private int _level = 0;
        private double _time = 0;
        public bool _invalid = false;
        private object _lock = new object();

        public RankedClient(uint clid)
        {
            _config = RankingBot.GetConfig();
            _clid = clid;
            _mysql = RankingBot.GetMySQL();
            _clinfo = new ClientInfoCommand(_clid).Execute(RankingBot._ts3);
            RankingBot.Log("Started tracking new client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ")", ConsoleColor.Cyan);
            CheckExistsInDB();
            SetGroups();
            _timer = new Timer(DoRank, null, 60000, 60000);
        }

        private void CheckExistsInDB()
        {
            if (_clinfo.UniqueId == null || _invalid)
            {
                Invalidate();
                return;
            }
            OpenDB();
            MySqlCommand c = new MySqlCommand("select * from " + _config.database.prefix + "users where uuid = '" + MySqlHelper.EscapeString(_clinfo.UniqueId) + "'", _mysql);
            MySqlDataReader r = c.ExecuteReader();
            if (!r.HasRows)
            {
                r.Close();
                c.CommandText = "insert into " + _config.database.prefix + "users (uuid, cur_level, cur_time) values ('" + MySqlHelper.EscapeString(_clinfo.UniqueId) + "', 0, 0)";
                if (c.ExecuteNonQuery() != 1)
                {
                    RankingBot.Log("ERROR - MySQL - Could not create new entry for client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ")", ConsoleColor.Red);
                }
                else
                {
                    CheckExistsInDB();
                }
            }
            else
            {
                while (r.Read())
                {
                    _dbId = (int)r["id"];
                    _level = (int)r["cur_level"];
                    _time = Convert.ToDouble(r["cur_time"]);
                }
                r.Close();
            }
            r.Close();
            CloseDB();
        }

        private void DoRank(Object o)
        {
            lock (_lock)
            {
                if (_clinfo.UniqueId == null || _invalid)
                {
                    Invalidate();
                    return;
                }

                RankingBot.Debug("DEBUG - RANK - DoRank() for client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ") [" + _dbId + "]");
                try
                {
                    RankingBot.Debug("DEBUG - RANK - Try get Client Info for client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ") [" + _dbId + "] @DoRank");
                    new ClientUpdateCommand(new ClientModification() { Nickname = "Ranking Bot" }).Execute(RankingBot._ts3);
                    ClientInfoCommandResponse clinfo = new ClientInfoCommand(_clid).Execute(RankingBot._ts3);
                    if (clinfo.IsErroneous || clinfo.DatabaseId != _clinfo.DatabaseId)
                    {
                        RankingBot.Log("ERROR - TS3 - Received erroneous ClientInfo data: " + clinfo.ResponseText + ". Retrying shortly... [" + _dbId + "]", ConsoleColor.Red);
                        Thread.Sleep(new Random().Next(1000, 10000));
                        DoRank(new object());
                    }
                    else
                    {
                        _clinfo = clinfo;
                    }
                    RankingBot.Debug("DEBUG - RANK - Got Client Info for client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ") [" + _dbId + "] @DoRank");
                }
                catch (Exception e)
                {
                    RankingBot.Log("ERROR - TS3 - " + e.Message, ConsoleColor.Red);
                }

                if (_config.clients.member_group != null)
                {
                    if (!_clinfo.ServerGroups.Contains((uint)_config.clients.member_group))
                    {
                        RankingBot.Debug("DEBUG - RANK - Add to member group for client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ") [" + _dbId + "]");
                        new ClientUpdateCommand(new ClientModification() { Nickname = "Ranking Bot" }).Execute(RankingBot._ts3);
                        if (new ServerGroupAddClientCommand((uint)_config.clients.member_group, _clinfo.DatabaseId).Execute(RankingBot._ts3).IsErroneous)
                        {
                            RankingBot.Log("ERROR - TS3 - Failed to add client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ") to ServerGroup " + _config.clients.member_group, ConsoleColor.Red);
                        }
                    }
                }

                if (_clinfo.IdleTime < TimeSpan.FromMinutes((double)_config.ranking.max_idle_time))
                {
                    _time++;
                    RankingBot.Debug("DEBUG - RANK - Client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ") now at time " + _time + " [" + _dbId + "]");
                    double nextLevelTime = Math.Round(15 * (Math.Pow(1.55, _level)));
                    if (_time >= nextLevelTime)
                    {
                        RankingBot.Debug("DEBUG - RANK - Level up for client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ") [" + _dbId + "]");
                        _level++;
                        RankingBot.Log("Client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ") has reached level " + _level, ConsoleColor.Cyan);
                        OpenDB();
                        MySqlCommand c = new MySqlCommand("update " + _config.database.prefix + "users set cur_time = " + _time + ", cur_level = " + _level + " where id = " + _dbId, _mysql);
                        if (c.ExecuteNonQuery() != 1)
                        {
                            RankingBot.Log("ERROR - MySQL - Failed to update time and level in database for client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ")", ConsoleColor.Red);
                        }
                        CloseDB();
                    }
                    else
                    {
                        OpenDB();
                        MySqlCommand c = new MySqlCommand("update " + _config.database.prefix + "users set cur_time = " + _time + " where id = " + _dbId, _mysql);
                        if (c.ExecuteNonQuery() != 1)
                        {
                            RankingBot.Log("ERROR - MySQL - Failed to update time in database for client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ")", ConsoleColor.Red);
                        }
                        CloseDB();
                    }
                }
                SetGroups();
            }
        }

        public void SetLevel(int level)
        {
            SetTime(Math.Round(15 * (Math.Pow(1.55, level - 1))) + 1);
        }

        public void SetTime(double time)
        {
            if (_clinfo.UniqueId == null || _invalid)
            {
                Invalidate();
                return;
            }

            _time = time;
            int newLevel = Convert.ToInt32(Math.Floor(Math.Log(_time / 15, 1.55))) + 1;
            if (_time < 15)
            {
                newLevel = 0;
            }
            if (_level != newLevel)
            {
                _level = newLevel;
                RankingBot.Log("Client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ") has been set to level " + _level, ConsoleColor.Cyan);
                OpenDB();
                MySqlCommand c = new MySqlCommand("update " + _config.database.prefix + "users set cur_time = " + _time + ", cur_level = " + _level + " where id = " + _dbId, _mysql);
                if (c.ExecuteNonQuery() != 1)
                {
                    RankingBot.Log("ERROR - MySQL - Failed to update time and level in database for client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ")", ConsoleColor.Red);
                }
                CloseDB();
                SetGroups();
            }
            else
            {
                RankingBot.Log("Client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ") has been set to time " + _time, ConsoleColor.Cyan);
                OpenDB();
                MySqlCommand c = new MySqlCommand("update " + _config.database.prefix + "users set cur_time = " + _time + " where id = " + _dbId, _mysql);
                if (c.ExecuteNonQuery() != 1)
                {
                    RankingBot.Log("ERROR - MySQL - Failed to update time in database for client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ")", ConsoleColor.Red);
                }
                CloseDB();
            }
        }

        public void Dispose()
        {
            _timer.Dispose();
            RankingBot.Log("Stopped tracking disconnected client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ")", ConsoleColor.Cyan);
        }

        public void SetGroups()
        {
            lock (_lock)
            {
                if (_clinfo.UniqueId == null || _invalid)
                {
                    Invalidate();
                    return;
                }

                RankingBot.Debug("DEBUG - RANK - SetGroups() for client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ") [" + _dbId + "]");
                try
                {
                    new ClientUpdateCommand(new ClientModification() { Nickname = "Ranking Bot" }).Execute(RankingBot._ts3);
                    int add = -1;
                    int[] groups = _config.ranking.ranks.ToObject<int[]>();
                    List<int> groupsList = new List<int>(groups);
                    if (groupsList.Count != 0)
                    {
                        if (groupsList.Count >= _level - 1 && _level != 0)
                        {
                            add = groupsList[_level - 1];
                            RankingBot.Debug("DEBUG - RANK - Needs group " + add + " [" + _dbId + "]");
                            groupsList.RemoveAt(_level - 1);
                        }
                        else if (_level != 0)
                        {
                            add = groupsList[groupsList.Count - 1];
                            RankingBot.Debug("DEBUG - RANK - Adding to last group " + add + "  [" + _dbId + "]");
                            groupsList.RemoveAt(groupsList.Count - 1);
                        }
                        groups = groupsList.ToArray();
                    }

                    if (!_clinfo.ServerGroups.Contains((uint)add) && add != -1)
                    {
                        RankingBot.Debug("DEBUG - RANK - Add to missing group " + add + " [" + _dbId + "]");
                        if (new ServerGroupAddClientCommand((uint)add, _clinfo.DatabaseId).Execute(RankingBot._ts3).IsErroneous)
                        {
                            RankingBot.Log("ERROR - TS3 - Failed to add client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ") to ServerGroup " + add + " [" + _dbId + "]", ConsoleColor.Red);
                        }
                    }

                    foreach (int group in groups)
                    {
                        if (_clinfo.ServerGroups.Contains((uint)group))
                        {
                            RankingBot.Debug("DEBUG - RANK - Remove from group " + group + " [" + _dbId + "]");
                            if (new ServerGroupDelClientCommand((uint)group, _clinfo.DatabaseId).Execute(RankingBot._ts3).IsErroneous)
                            {
                                RankingBot.Log("ERROR - TS3 - Failed to remove client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ") from ServerGroup " + group + " [" + _dbId + "]", ConsoleColor.Red);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    RankingBot.Log("ERROR - TS3 - " + e.Message, ConsoleColor.Red);
                }
            }
        }

        private void OpenDB()
        {
            if (_mysql.State != System.Data.ConnectionState.Open)
            {
                RankingBot.Debug("DEBUG - CORE - OpenDB() [" + _dbId + "]");
                _mysql = new MySqlConnection("server=" + _config.database.server + ";database=" + _config.database.dbname + ";uid=" + _config.database.username + ";password=" + _config.database.password);
                try
                {
                    _mysql.Open();
                }
                catch (MySqlException e)
                {
                    RankingBot.Log("ERROR - MySQL - " + e.Message, ConsoleColor.Red);
                }
            }
        }

        private void CloseDB()
        {
            if (_mysql.State != System.Data.ConnectionState.Closed)
            {
                RankingBot.Debug("DEBUG - CORE - CloseDB() [" + _dbId + "]");
                _mysql.Close();
            }
        }

        private void Invalidate()
        {
            _invalid = true;
            CloseDB();
            Dispose();
            RankingBot.Log("ERROR - TS3 - Client connection with ID " + _clid + " has become invalid.", ConsoleColor.Red);
        }
    }
}
