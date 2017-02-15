using Newtonsoft.Json;
using MySql.Data.MySqlClient;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using TS3QueryLib.Net.Core;

namespace TS3_Ranking_Bot
{
    public class RankingBot
    {
        private static int _instanceId = 0;
        private static dynamic _config;
        private static MySqlConnection _mysql;
        private static StreamWriter _logger;
        public static QueryClient _ts3;
        public static bool _debug = true;
        public static string _debugger = null;

        public static void Main(string[] args)
        {
            ReadConfigFile();
            InitDatabase();
        }

        private static void ReadConfigFile()
        {
            if (!File.Exists("config.json"))
            {
                Environment.Exit(1);
            }
            _config = JsonConvert.DeserializeObject(File.ReadAllText("config.json"));
            int[] ranks = _config.ranking.ranks.ToObject<int[]>();
            if (ranks.Length == 0)
            {
                
            }
        }

        private static void InitDatabase()
        {
            _mysql = new MySqlConnection("server=" + _config.database.server + ";database=" + _config.database.dbname + ";uid=" + _config.database.username + ";password=" + _config.database.password);
            try
            {
                _mysql.Open();
            }
            catch (MySqlException e)
            {

            }
            VerifyTables();
        }

        private static void VerifyTables()
        {
            MySqlDataReader r;
            bool verified = true;
            string[] tables = new string[] { "users" };
            foreach (string tbl in tables)
            {
                r = ExecuteCommand("show columns from " + _config.database.prefix + tbl);
                if (r == null)
                {
                    verified = false;
                }
                else
                {
                    r.Close();
                }
            }

            if (!verified)
            {
                using (StreamReader sr = new StreamReader(Assembly.Load(new AssemblyName("TS3_Ranking_Bot")).GetManifestResourceStream("TS3_Ranking_Bot.init.sql")))
                {
                    string sql = sr.ReadToEnd();
                    sql = sql.Replace("{{prefix}}", (string)_config.database.prefix);
                    MySqlDataReader res = ExecuteCommand(sql);
                    if (res == null)
                    {
                        Environment.Exit(1);
                    }
                    res.Close();
                }
            }
        }

        private static MySqlDataReader ExecuteCommand(string cmd)
        {
            try
            {
                MySqlCommand c = new MySqlCommand(cmd, _mysql);
                return c.ExecuteReader();
            }
            catch (MySqlException e)
            {
                return null;
            }
        }
    }
}