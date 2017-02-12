using Newtonsoft.Json;
using MySql.Data.MySqlClient;
using System;
using System.IO;
using System.Reflection;

namespace TS3_Ranking_Bot
{
    public class RankingBot
    {
        private static int _instanceId = 0;
        private static dynamic _config;
        private static MySqlConnection _mysqlCon;

        public static void Main(string[] args)
        {
            Log("Initialised TS3RankingBot");
            ReadConfigFile();
            InitDatabase();
        }

        private static void ReadConfigFile()
        {
            if (!File.Exists("config.json"))
            {
                Log("ERROR - Please copy config.example.json to config.json and edit it according to your setup.", ConsoleColor.Red);
                Environment.Exit(1);
            }
            _config = JsonConvert.DeserializeObject(File.ReadAllText("config.json"));
            Log("Config File Parsed", ConsoleColor.Green);
        }

        private static void InitDatabase()
        {
            _mysqlCon = new MySqlConnection("server=" + _config.database.server + ";database=" + _config.database.dbname + ";uid=" + _config.database.username + ";password=" + _config.database.password);
            try
            {
                _mysqlCon.Open();
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
            MySqlDataReader r;
            bool verified = false;
            string[] tables = new string[] { "users" };
            foreach (string tbl in tables)
            {
                Log("Checking table " + _config.database.prefix + tbl, ConsoleColor.Yellow);
                r = ExecuteCommand("show columns from " + _config.database.prefix + tbl);
                if (r == null)
                {
                    verified = false;
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
                    Log("Tables created!", ConsoleColor.Green);
                }
            }
        }

        private static void Log(string msg, ConsoleColor col = ConsoleColor.White)
        {
            if (msg != null)
            {
                if (!Directory.Exists("logs"))
                {
                    Directory.CreateDirectory("logs");
                }

                string DateStamp = "[" + DateTime.Now.ToString() + "] ";
                Console.WriteLine(DateStamp + msg);
                File.AppendAllText(@"logs/" + GetLogFile(), DateStamp + msg + Environment.NewLine);
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
            try
            {
                MySqlCommand c = new MySqlCommand(cmd, _mysqlCon);
                return c.ExecuteReader();
            }
            catch (MySqlException e)
            {
                Log("ERROR - MySQL - " + e.Message, ConsoleColor.Red);
                return null;
            }
        }
    }
}