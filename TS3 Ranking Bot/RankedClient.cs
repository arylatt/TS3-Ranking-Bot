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
        public uint _clid;
        private MySqlConnection _mysql;
        private Timer _timer;
        private dynamic _config;
        private int _dbId;
        private int _level;
        private double _time;

        public RankedClient(uint clid)
        {
            _config = RankingBot.GetConfig();
            _clid = clid;
            _mysql = RankingBot.GetMySQL();
            _clinfo = new ClientInfoCommand(_clid).Execute(RankingBot._ts3);
            RankingBot.Log("Started tracking new client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ")");
            CheckExistsInDB();
            CleanGroups();
            _timer = new Timer(DoRank, null, 60000, 60000);
        }

        private void CheckExistsInDB()
        {
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
        }

        private void DoRank(Object o)
        {
            _clinfo = new ClientInfoCommand(_clid).Execute(RankingBot._ts3);

            if (_config.clients.member_group != null)
            {
                if (!_clinfo.ServerGroups.Contains((uint)_config.clients.member_group))
                {
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
                double nextLevelTime = Math.Round(15 * (Math.Pow(1.55, _level)));
                if (_time >= nextLevelTime)
                {
                    _level++;
                    RankingBot.Log("Client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ") has reached level " + _level, ConsoleColor.Cyan);
                    int[] ranks = _config.ranking.ranks.ToObject<int[]>();
                    if (ranks.Length >= _level)
                    {
                        new ClientUpdateCommand(new ClientModification() { Nickname = "Ranking Bot" }).Execute(RankingBot._ts3);
                        if (new ServerGroupAddClientCommand((uint)ranks[_level - 1], _clinfo.DatabaseId).Execute(RankingBot._ts3).IsErroneous)
                        {
                            RankingBot.Log("ERROR - TS3 - Failed to add client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ") to ServerGroup " + ranks[_level - 1], ConsoleColor.Red);
                        }
                    }
                    else
                    {
                        if (ranks.Length != 0)
                        {
                            if (!_clinfo.ServerGroups.Contains((uint)ranks[ranks.Length - 1]))
                            {
                                new ClientUpdateCommand(new ClientModification() { Nickname = "Ranking Bot" }).Execute(RankingBot._ts3);
                                if (new ServerGroupAddClientCommand((uint)ranks[ranks.Length - 1], _clinfo.DatabaseId).Execute(RankingBot._ts3).IsErroneous)
                                {
                                    RankingBot.Log("ERROR - TS3 - Failed to add client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ") to ServerGroup " + ranks[ranks.Length - 1], ConsoleColor.Red);
                                }
                            }
                        }
                        RankingBot.Log("WARN - TS3 - Unable to give new ServerGroup to client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ") as there are no new groups to assign", ConsoleColor.Yellow);
                    }

                    MySqlCommand c = new MySqlCommand("update " + _config.database.prefix + "users set cur_time = " + _time + ", cur_level = " + _level + " where id = " + _dbId, _mysql);
                    if (c.ExecuteNonQuery() != 1)
                    {
                        RankingBot.Log("ERROR - MySQL - Failed to update time and level in database for client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ")", ConsoleColor.Red);
                    }
                }
                else
                {
                    MySqlCommand c = new MySqlCommand("update " + _config.database.prefix + "users set cur_time = " + _time + " where id = " + _dbId, _mysql);
                    if (c.ExecuteNonQuery() != 1)
                    {
                        RankingBot.Log("ERROR - MySQL - Failed to update time in database for client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ")", ConsoleColor.Red);
                    }
                }
            }
            CleanGroups();
        }

        public void SetLevel(int level)
        {
            SetTime(Math.Round(15 * (Math.Pow(1.55, level - 1))) + 1);
        }

        public void SetTime(double time)
        {
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
                int[] ranks = _config.ranking.ranks.ToObject<int[]>();
                if (ranks.Length >= _level && _level != 0)
                {
                    new ClientUpdateCommand(new ClientModification() { Nickname = "Ranking Bot" }).Execute(RankingBot._ts3);
                    if (new ServerGroupAddClientCommand((uint)ranks[_level - 1], _clinfo.DatabaseId).Execute(RankingBot._ts3).IsErroneous)
                    {
                        RankingBot.Log("ERROR - TS3 - Failed to add client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ") to ServerGroup " + ranks[_level - 1], ConsoleColor.Red);
                    }
                }
                else if(_level != 0)
                {
                    if (ranks.Length != 0)
                    {
                        if (!_clinfo.ServerGroups.Contains((uint)ranks[ranks.Length - 1]))
                        {
                            new ClientUpdateCommand(new ClientModification() { Nickname = "Ranking Bot" }).Execute(RankingBot._ts3);
                            if (new ServerGroupAddClientCommand((uint)ranks[ranks.Length - 1], _clinfo.DatabaseId).Execute(RankingBot._ts3).IsErroneous)
                            {
                                RankingBot.Log("ERROR - TS3 - Failed to add client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ") to ServerGroup " + ranks[ranks.Length - 1], ConsoleColor.Red);
                            }
                        }
                    }
                    RankingBot.Log("WARN - TS3 - Unable to give new ServerGroup to client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ") as there are no new groups to assign", ConsoleColor.Yellow);
                }

                MySqlCommand c = new MySqlCommand("update " + _config.database.prefix + "users set cur_time = " + _time + ", cur_level = " + _level + " where id = " + _dbId, _mysql);
                if (c.ExecuteNonQuery() != 1)
                {
                    RankingBot.Log("ERROR - MySQL - Failed to update time and level in database for client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ")", ConsoleColor.Red);
                }
                CleanGroups();
            }
            else
            {
                RankingBot.Log("Client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ") has been set to time " + _time, ConsoleColor.Cyan);
                MySqlCommand c = new MySqlCommand("update " + _config.database.prefix + "users set cur_time = " + _time + " where id = " + _dbId, _mysql);
                if (c.ExecuteNonQuery() != 1)
                {
                    RankingBot.Log("ERROR - MySQL - Failed to update time in database for client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ")", ConsoleColor.Red);
                }
            }
        }

        public void Dispose()
        {
            _timer.Dispose();
            RankingBot.Log("Stopped tracking disconnected client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ")");
        }

        public void CleanGroups()
        {
            _clinfo = new ClientInfoCommand(_clid).Execute(RankingBot._ts3);
            new ClientUpdateCommand(new ClientModification() { Nickname = "Ranking Bot" }).Execute(RankingBot._ts3);
            int add = -1;
            int[] groups = _config.ranking.ranks.ToObject<int[]>();
            List<int> groupsList = new List<int>(groups);
            if (groupsList.Count != 0)
            {
                if (groupsList.Count >= _level && _level != 0)
                {
                    add = groupsList[_level - 1];
                    groupsList.RemoveAt(_level - 1);
                }
                else
                {
                    add = groupsList[groupsList.Count - 1];
                    groupsList.RemoveAt(groupsList.Count - 1);
                }
                groups = groupsList.ToArray();
            }

            if (!_clinfo.ServerGroups.Contains((uint)add))
            {
                if (new ServerGroupAddClientCommand((uint)add, _clinfo.DatabaseId).Execute(RankingBot._ts3).IsErroneous)
                {
                    RankingBot.Log("ERROR - TS3 - Failed to remove client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ") from ServerGroup " + add, ConsoleColor.Red);
                }
            }

            foreach (int group in groups)
            {
                if (_clinfo.ServerGroups.Contains((uint)group))
                {
                    if (new ServerGroupDelClientCommand((uint)group, _clinfo.DatabaseId).Execute(RankingBot._ts3).IsErroneous)
                    {
                        RankingBot.Log("ERROR - TS3 - Failed to remove client '" + _clinfo.Nickname + "' (" + _clinfo.UniqueId + ") from ServerGroup " + group, ConsoleColor.Red);
                    }
                }
            }
        }
    }
}
