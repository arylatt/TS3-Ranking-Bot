using Newtonsoft.Json;
using MySql.Data.MySqlClient;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using TS3QueryLib.Net.Core;
using TS3QueryLib.Net.Core.Server.Commands;
using TS3QueryLib.Net.Core.Server.Notification;
using TS3QueryLib.Net.Core.Server.Responses;

namespace TS3_Ranking_Bot
{
    public class RankingBot
    {
        public static Logger Logger { get; protected set; }
        public static bool Debugging { get; protected set; } = false;
        public static string Debugger { get; protected set; } = null;
        public static bool DebugRank { get; protected set; } = false;
        public static MySqlConnection MySQL { get; protected set; }
        public static QueryClient TS3 { get; protected set; }
        public static TS3Handler TS3Handler { get; protected set; }
        public static dynamic Config { get; protected set; }

        private static string _file = "[RankingBot]";
        public static object TS3Lock = new object();

        public static void Main(string[] args)
        {
            Logger = new Logger();
            ReadConfigFile();
            InitDatabase();
            TS3Handler = new TS3Handler();
        }

        private static void ReadConfigFile()
        {
            Logger.Debug(_file + " Begin reading config");
            if (!File.Exists("config.json"))
            {
                Logger.Error(_file + " Please copy config.example.json to config.json and edit according to your setup");
                Environment.Exit(1);
            }
            Config = JsonConvert.DeserializeObject(File.ReadAllText("config.json"));
            int[] ranks = Config.ranking.ranks.ToObject<int[]>();
            if (ranks.Length == 0)
            {
                Logger.Warn(_file + " There are no ranks defined in the config file.");
            }
            if (Config.debug != null)
            {
                if (Config.debug.debugging != null)
                {
                    Debugging = (bool)Config.debug.debugging;
                }
                if (Config.debug.debugger != null)
                {
                    Debugger = (string)Config.debug.debugger;
                }
                if (Config.debug.rank != null)
                {
                    DebugRank = (bool)Config.debug.rank;
                }
            }
            Logger.Debug(_file + " Finish reading config");
        }

        private static void InitDatabase()
        {
            Logger.Debug(_file + " Begin init database");
            MySQL = new MySqlConnection("server=" + Config.database.server + ";database=" + Config.database.dbname + ";uid=" + Config.database.username + ";password=" + Config.database.password);
            try
            {
                MySQL.Open();
                Logger.Info(_file + " Connected to DB '" + Config.database.dbname + "' on host '" + Config.database.server + "'");
            }
            catch (MySqlException e)
            {
                Logger.Error(_file + " Could not connect to MySQL Database: " + e.Message);
                Environment.Exit(1);
            }
            VerifyTables();
            MySQL.Close();
            Logger.Debug(_file + " Finish init database");
        }

        private static void VerifyTables()
        {
            Logger.Debug(_file + " Begin verify tables");
            MySqlDataReader r;
            bool verified = true;
            string[] tables = new string[] { "users" };
            foreach (string tbl in tables)
            {
                r = ExecuteCommand("show columns from " + Config.database.prefix + tbl);
                if (r == null)
                {
                    Logger.Warn(_file + " Missing table " + Config.database.prefix + tbl);
                    verified = false;
                }
                else
                {
                    r.Close();
                }
            }

            if (!verified)
            {
                Logger.Info(_file + " Some tables are missing, recreating...");
                using (StreamReader sr = new StreamReader(Assembly.Load(new AssemblyName("TS3_Ranking_Bot")).GetManifestResourceStream("TS3_Ranking_Bot.init.sql")))
                {
                    string sql = sr.ReadToEnd();
                    sql = sql.Replace("{{prefix}}", (string)Config.database.prefix);
                    MySqlDataReader res = ExecuteCommand(sql);
                    if (res == null)
                    {
                        Environment.Exit(1);
                    }
                    res.Close();
                }
            }
            Logger.Debug(_file + " Finish verify tables");
        }

        private static MySqlDataReader ExecuteCommand(string cmd)
        {
            try
            {
                MySqlCommand c = new MySqlCommand(cmd, MySQL);
                return c.ExecuteReader();
            }
            catch (MySqlException e)
            {
                Logger.Debug(_file + " Error executing query '" + cmd + "': " + e.Message);
                return null;
            }
        }

        public static void ConnectTS3(string ip, ushort port, NotificationHub notify)
        {
            TS3 = new QueryClient(ip, port, notify);
            Logger.Debug(_file + " Created new QueryClient");
        }
    }
}