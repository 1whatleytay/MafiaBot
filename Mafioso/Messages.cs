using System;
using System.Data;
using System.Reflection;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Microsoft.Extensions.DependencyInjection;

namespace Mafioso {
    public class Messages {
        // Config
        private const char Prefix = '-';
        private const bool ShowStackTrace = true;

        private readonly DiscordSocketClient _client;
        
        private readonly CommandService _commands;
        private readonly ServiceProvider _services;
        
        // Called by Discord.Net when it wants to log something.
        private static Task Log(LogMessage message) {
            Console.WriteLine(message.Message);
            return Task.CompletedTask;
        }
        
        // Called by Discord.Net when the bot receives a message.
        private async Task CheckMessage(SocketMessage message) {
            if (!(message is SocketUserMessage userMessage)) return;

            var prefixStart = 0;

            if (userMessage.HasCharPrefix(Prefix, ref prefixStart)) {
                // Create Context and Execute Commands
                var context = new SocketCommandContext(_client, userMessage);
                var result = await _commands.ExecuteAsync(context, prefixStart, _services);
                
                // Handle any errors.
                if (!result.IsSuccess && result.Error != CommandError.UnknownCommand) {
                    if (ShowStackTrace && result.Error == CommandError.Exception
                                       && result is ExecuteResult execution) {
                        await userMessage.Channel.SendMessageAsync(
                            Utils.Code(execution.Exception.Message + "\n\n" + execution.Exception.StackTrace));
                    } else {
                        await userMessage.Channel.SendMessageAsync(
                            "Halt! We've hit an error." + Utils.Code(result.ErrorReason));
                    }
                }
            }
        }

        // Initializes the Message Handler, subscribe to events, etc.
        public async Task Init() {
            _client.Log += Log;
            _client.MessageReceived += CheckMessage;
            
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }
        
        public Messages(DiscordSocketClient client) {
            _client = client;
            
            _commands = new CommandService();
            _services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_commands)
                .BuildServiceProvider();
        }
    }
}