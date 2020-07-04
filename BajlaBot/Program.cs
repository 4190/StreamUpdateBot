using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.IO;
using System.Collections.Generic;

using Newtonsoft.Json;
using Discord;
using Discord.WebSocket;
using Discord.Commands;

namespace Disc
{
    class Program
    {
        public Dictionary<string, string> configDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText($"config.json"));
        private DiscordSocketClient Client;
        private CommandService Commands;
        private bool isOnlineNow = false;
        private IMessageChannel channel;

        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();


        private async Task MainAsync()
        {
            Client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Debug
            });

            Commands = new CommandService(new CommandServiceConfig
            {
                CaseSensitiveCommands = false,
                DefaultRunMode = RunMode.Async,
                LogLevel = LogSeverity.Debug
            });


            await Commands.AddModulesAsync(Assembly.GetEntryAssembly(), null);
            Client.Ready += Client_Ready;
            Client.Log += Client_Log;
            Client.MessageReceived += Client_MessageReceived;
            Client.LatencyUpdated += Client_LatencyUpdated;

            //bot token- KEEP IT PRIVATE
            //load from config for public bot
            string Token = configDict["botSecretToken"];
            // string testToken = "";
            await Client.LoginAsync(TokenType.Bot, Token);
            await Client.StartAsync();


            await Task.Delay(-1);       //bot not shutting down after idling for too long
        }

        private async Task Client_Log(LogMessage Message)
        {
            Console.WriteLine($"{DateTime.Now} at {Message.Source}] {Message.Message}");
        }

        private async Task Client_Ready()
        {
            await Client.SetGameAsync("Ina", "https://www.google.com/");
            channel = Client.GetChannel(id: 653622429456924673) as IMessageChannel;

            foreach (string s in configDict.Keys)
            {
                Console.WriteLine($"{s} : {configDict[s]}");
            }
        }

        private async Task Client_LatencyUpdated(int arg1, int arg2)
        {
            if (channel != null) { await AnnounceStreamStatusChange(); }
        }
        public async Task AnnounceStreamStatusChange()
        {
            bool lastCheckOnlineStatus = isOnlineNow; //initially false
            isOnlineNow = await IsStreamOnline(isOnlineNow,
                configDict["streamname"],
                configDict["twitchClientId"]);
            try
            {
                if (lastCheckOnlineStatus != isOnlineNow)
                {
                    if (isOnlineNow) //execute if when stream status changed from offline to online
                    {
                        Console.WriteLine("STREAM JUST WENT ONLINE");
                        await channel.SendMessageAsync("Hejka @everyone  InaBajlando zaczęła strimka, bierz herbatkę i wpadaj :wink:   https://www.twitch.tv/inabajlando");
                    }
                    else if (!isOnlineNow) //execute if when stream status changed from online to offline
                    {
                        Console.WriteLine("STREAM JUST WENT OFFLINE");
                        await channel.SendMessageAsync("InaBajlando dziękuje za dzisiejszego strima! Do zobaczenia na kolejnym :slight_smile:");
                    }
                }
                else
                {
                    Console.WriteLine("status unchanged");
                }
            }
            catch (Exception e) { Console.WriteLine(e); }
        }

        private async Task Client_MessageReceived(SocketMessage MessageParam)
        {
            var Message = MessageParam as SocketUserMessage;
            var Context = new SocketCommandContext(Client, Message);
            if (Context.Message == null || Context.Message.Content == "")
            {
                Console.WriteLine(Context.Message.Content);
                return;
            }
            if (Context.User.IsBot) return;

            int ArgPos = 0;
            if (!(Message.HasStringPrefix("a!", ref ArgPos) || Message.HasMentionPrefix(Client.CurrentUser, ref ArgPos)))
            {
                return;
            }

            var Result = await Commands.ExecuteAsync(Context, ArgPos, null);

            if (!Result.IsSuccess)
            {
                Console.WriteLine($"{DateTime.Now} at Commands] Something went wrong with executing a command. Text: {Context.Message.Content} | Error: {Result.ErrorReason}");
            }
        }

        public static async Task<bool> IsStreamOnline(bool wasOnline, string streamName, string clientId)
        {
            HttpClientHandler hcHandle = new HttpClientHandler();
            using (var hc = new HttpClient(hcHandle, false))
            {
                hc.DefaultRequestHeaders.Add("Client-ID", clientId);

                using (var response = await hc.GetAsync($"https://api.twitch.tv/helix/streams?user_login={streamName}")) //user_login=PasteYourChannelNameHere
                {
                    string jsonString = await response.Content.ReadAsStringAsync();

                    bool isStreamOnline = jsonString.Contains("\"type\":\"live\""); //only checking for "live" in response
                    //Console.WriteLine(jsonString);

                    //check if stream is online only if previous check returned offline
                    if (isStreamOnline && !wasOnline)
                    {
                        Console.WriteLine("live");
                        return true;
                    }
                    //check if stream is offline only if previous check returned online
                    else if (!isStreamOnline && wasOnline)
                    {
                        Console.WriteLine("off");
                        return false;
                    }
                    else  //if online status since last check didn't change
                    {
                        if (wasOnline) { return true; }
                        else { return false; }
                    }
                    //can use JSON.NET library and check for other info in response
                }
            }
        }
    }
}