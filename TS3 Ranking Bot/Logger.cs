using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace TS3_Ranking_Bot
{
    public class Logger
    {
        protected Queue<LogItem> _logQueue = new Queue<LogItem>();
        protected AutoResetEvent _newItems = new AutoResetEvent(false);
        protected bool _logging = true;
        protected StreamWriter _logger;

        public Logger()
        {
            InitLogs();
            Thread logger = new Thread(new ThreadStart(ProcessLogs));
            logger.IsBackground = true;
            logger.Start();
        }

        protected void ProcessLogs()
        {
            while (_logging)
            {
                _newItems.WaitOne(1000);
                Queue<LogItem> logQueue;
                lock (_logQueue)
                {
                    logQueue = new Queue<LogItem>(_logQueue);
                    _logQueue.Clear();
                }

                foreach (LogItem log in logQueue)
                {
                    DoLog(log);
                }
            }
        }

        protected void InitLogs()
        {
            if (!Directory.Exists("logs"))
            {
                Directory.CreateDirectory("logs");
            }
            string date = DateTime.Today.ToString("yyyy-MM-ddex");
            string logFile = @"logs/" + date + "_" + (Directory.GetFiles(@"logs/", date + "_*.log").Length + 1).ToString() + ".log";
            _logger = new StreamWriter(File.Open(logFile, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite), Encoding.ASCII, 4096, true);
        }

        protected void DoLog(LogItem log)
        {
            using (_logger)
            {
                Console.ForegroundColor = log.Color;
                Console.WriteLine(log.Message);
                Console.ForegroundColor = ConsoleColor.White;
                _logger.WriteLine(log.Message);
            }
        }

        protected void StopLogging()
        {
            _logging = false;
        }

        public void Log(string msg, ConsoleColor col = ConsoleColor.White)
        {
            LogItem log = new LogItem(msg, col);
            lock (_logQueue)
            {
                _logQueue.Enqueue(log);
            }
            _newItems.Set();
        }

        public void Info(string msg)
        {
            Log("[INFO]  " + msg, ConsoleColor.Cyan);
        }

        public void Warn(string msg)
        {
            Log("[WARN]  " + msg, ConsoleColor.Yellow);
        }

        public void Error(string msg)
        {
            Log("[ERROR] " + msg, ConsoleColor.Red);
        }

        public void Debug(string msg)
        {
            if (RankingBot.Debugging)
            {
                Log("[DEBUG] " + msg, ConsoleColor.Magenta);
            }
        }
    }

    public class LogItem
    {
        public string Message { get; protected set; }
        public ConsoleColor Color { get; protected set; }

        public LogItem(string msg, ConsoleColor col)
        {
            Message = "[" + DateTime.Now.ToString() + "] " + msg;
            Color = col;
        }
    }
}
