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
        }

        private static void Log(string msg, ConsoleColor col = ConsoleColor.White)
        {
            if (!Directory.Exists("logs"))
            {
                Directory.CreateDirectory("logs");
            }

            string DateStamp = "[" + DateTime.Now.ToString() + "] ";
            Console.WriteLine(DateStamp + msg);
            File.AppendAllText(@"logs/" + GetLogFile(), DateStamp + msg + Environment.NewLine);
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
    }
}