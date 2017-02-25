using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using TS3QueryLib.Net.Core.Server.Commands;

namespace TS3_Ranking_Bot
{
    class UpdateChecker
    {
        private string _file = "[UpdateChkr]";
        private string _ver;
        private Timer _timer;

        public UpdateChecker()
        {
            _ver = Assembly.Load(new AssemblyName("TS3_Ranking_Bot")).GetName().Version.ToString();
            _timer = new Timer(CheckForUpdates, null, 0, 86400000);
        }

        private async void CheckForUpdates(object o)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    client.BaseAddress = new Uri("https://raw.githubusercontent.com");
                    var resp = await client.GetAsync("/arylatt/TS3-Ranking-Bot/master/latest.json");
                    resp.EnsureSuccessStatusCode();
                    var json = await resp.Content.ReadAsStringAsync();
                    dynamic info = JsonConvert.DeserializeObject(json);
                    if (info.latest != null)
                    {
                        if (info.latest > _ver)
                        {
                            string msg = "";
                            if (info.message != null)
                            {
                                msg = info.message;
                            }

                            RankingBot.Logger.Info(_file + " Update Available (" + msg + ")");
                            new SendTextMessageCommand(TS3QueryLib.Net.Core.Common.CommandHandling.MessageTarget.Server, 0, "Update Available! " + msg).Execute(RankingBot.TS3);
                        }
                    }
                }
                catch (HttpRequestException e)
                {
                    RankingBot.Logger.Warn(_file + " Could not check for updates: " + e.Message);
                }
            }
        }
    }
}
