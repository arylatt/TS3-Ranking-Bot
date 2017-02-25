using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using TS3QueryLib.Net.Core.Server.Commands;

namespace TS3_Ranking_Bot
{
    class UpdateChecker
    {
        private string _file = "[Updater]   ";
        private string _ver;
        private Timer _timer;

        public UpdateChecker()
        {
            _ver = "v" + Assembly.Load(new AssemblyName("TS3_Ranking_Bot")).GetName().Version.ToString();
            _timer = new Timer(CheckForUpdates, null, 0, 86400000);
        }

        private async void CheckForUpdates(object o)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    client.BaseAddress = new Uri("https://api.github.com");
                    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
                    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "TS3-Ranking-Bot");
                    var resp = await client.GetAsync("/repos/arylatt/TS3-Ranking-Bot/releases/latest");
                    resp.EnsureSuccessStatusCode();
                    var json = await resp.Content.ReadAsStringAsync();
                    dynamic info = JsonConvert.DeserializeObject(json);
                    if (info.tag_name != null)
                    {
                        if (info.tag_name > _ver)
                        {
                            string msg = "";
                            if (info.name != null)
                            {
                                msg = info.name;
                            }

                            RankingBot.Logger.Info(_file + " Update Available [" + _ver + "] -> [" + info.tag_name + "] (" + msg + ")");
                            new SendTextMessageCommand(TS3QueryLib.Net.Core.Common.CommandHandling.MessageTarget.Server, 0, "Update Available! " + msg).Execute(RankingBot.TS3);
                        }
                        else
                        {
                            RankingBot.Logger.Info(_file + " Currently up to date [" + info.tag_name + "]");
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
