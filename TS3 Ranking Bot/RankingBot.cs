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
        private static TS3Handler _ts3Hndl;
        private static StreamWriter _logger;
        public static QueryClient _ts3;
        public static bool _debug = true;
        public static string _debugger = null;

        public static void Main(string[] args)
        {
            LogInit();
            Debug("DEBUG - CORE - Enter Main()");
            Log("Initialised TS3RankingBot");
            ReadConfigFile();
            InitDatabase();
            _ts3Hndl = new TS3Handler();
        }

        public static void TryReconnect()
        {
            Debug("DEBUG - CORE - Enter TryReconnect()");
            Log("WARN - TS3 - Lost Connection. Attempting to reconnect in 10 seconds", ConsoleColor.Yellow);
            System.Threading.Thread.Sleep(10000);
            _ts3Hndl = new TS3Handler();
        }

        private static void ReadConfigFile()
        {
            Debug("DEBUG - CORE - Enter ReadConfigFile()");
            if (!File.Exists("config.json"))
            {
                Log("ERROR - Please copy config.example.json to config.json and edit it according to your setup.", ConsoleColor.Red);
                Environment.Exit(1);
            }
            _config = JsonConvert.DeserializeObject(File.ReadAllText("config.json"));
            Log("Config File Parsed", ConsoleColor.Green);
            int[] ranks = _config.ranking.ranks.ToObject<int[]>();
            if (ranks.Length == 0)
            {
                Log("WARN - CFG - There are no ranks configured in the config file. Please add some ServerGroup IDs to the ranks array", ConsoleColor.Yellow);
            }
        }

        private static void InitDatabase()
        {
            Debug("DEBUG - CORE - Enter InitDatabase()");
            _mysql = new MySqlConnection("server=" + _config.database.server + ";database=" + _config.database.dbname + ";uid=" + _config.database.username + ";password=" + _config.database.password);
            try
            {
                _mysql.Open();
            }
            catch (MySqlException e)
            {
                Log("ERROR - MySQL - " + e.Message, ConsoleColor.Red);
            }
            Log("Connected to database '" + _config.database.dbname + "' on '" + _config.database.server + "'", ConsoleColor.Green);
            VerifyTables();
        }

        private static void VerifyTables()
        {
            Debug("DEBUG - CORE - Enter VerifyTables()");
            MySqlDataReader r;
            bool verified = true;
            string[] tables = new string[] { "users" };
            foreach (string tbl in tables)
            {
                Log("Checking table " + _config.database.prefix + tbl, ConsoleColor.Yellow);
                r = ExecuteCommand("show columns from " + _config.database.prefix + tbl);
                if (r == null)
                {
                    Debug("DEBUG - MySQL - Empty Reader");
                    verified = false;
                }
                else
                {
                    Debug("DEBUG - MySQL - Closed Reader");
                    r.Close();
                }
            }

            if (!verified)
            {
                Log("Tables missing. Recreating...");
                using (StreamReader sr = new StreamReader(Assembly.Load(new AssemblyName("TS3_Ranking_Bot")).GetManifestResourceStream("TS3_Ranking_Bot.init.sql")))
                {
                    string sql = sr.ReadToEnd();
                    sql = sql.Replace("{{prefix}}", (string)_config.database.prefix);
                    MySqlDataReader res = ExecuteCommand(sql);
                    if (res == null)
                    {
                        Log("ERROR - MySQL - Failed to create tables...", ConsoleColor.Red);
                        Environment.Exit(1);
                    }
                    res.Close();
                    Log("Tables created!", ConsoleColor.Green);
                }
            }
        }

        private static void LogInit()
        {
            if (!Directory.Exists("logs"))
            {
                Directory.CreateDirectory("logs");
            }
            _logger = new StreamWriter(File.Open(@"logs/" + GetLogFile(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite), System.Text.Encoding.ASCII, 4096, true);
        }

        public static void Log(string msg, ConsoleColor col = ConsoleColor.White)
        {
            string DateStamp = "[" + DateTime.Now.ToString() + "] ";
            new Task(() => LogAsync(DateStamp + msg, col)).Start();
        }

        private static void LogAsync(string msg, ConsoleColor col)
        {
            using (_logger)
            {
                Console.ForegroundColor = col;
                Console.WriteLine(msg);
                _logger.WriteLine(msg);
            }
        }

        public static void Debug(string msg, ConsoleColor col = ConsoleColor.Magenta)
        {
            if (_debug)
            {
                Log(msg, col);
            }
        }

        private static string GetLogFile()
        {
            string date = DateTime.Today.GetDateTimeFormats()[4];
            if (_instanceId == 0)
            {
                _instanceId = Directory.GetFiles(@"logs/", date + "_*.log").Length + 1;
            }
            return date + "_" + _instanceId.ToString() + ".log";
        }

        private static MySqlDataReader ExecuteCommand(string cmd)
        {
            Debug("DEBUG - CORE - Enter ExecuteCommand(" + cmd + ")");
            try
            {
                MySqlCommand c = new MySqlCommand(cmd, _mysql);
                Debug("DEBUG - MySQL - Opened Reader");
                return c.ExecuteReader();
            }
            catch (MySqlException e)
            {
                Log("ERROR - MySQL - " + e.Message, ConsoleColor.Red);
                return null;
            }
        }

        public static dynamic GetConfig()
        {
            return _config;
        }

        public static MySqlConnection GetMySQL()
        {
            return _mysql;
        }
    }
}