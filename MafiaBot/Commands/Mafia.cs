using System.Collections.Generic;
using System.Threading.Tasks;

using Discord.Commands;
using Discord.WebSocket;

namespace MafiaBot.Commands {
    public class Mafia : ModuleBase<SocketCommandContext> {
        private static readonly Dictionary<ulong, MafiaContext> Games = new Dictionary<ulong, MafiaContext>();

        private static MafiaContext GetOrCreateGame(DiscordSocketClient client, ulong guildId) {
            if (Games.ContainsKey(guildId)) {
                return Games[guildId];
            }
            
            var context = new MafiaContext(client, guildId);
            Games[guildId] = context;
            return context;
        }

        private static MafiaContext GetOrCreateGame(SocketCommandContext context) {
            return GetOrCreateGame(context.Client, context.Guild.Id);
        }
        
        [Command("create")]
        [Summary("Creates a game lobby.")]
        public async Task Create() {
            var game = GetOrCreateGame(Context);
            await game.Create();
        }
        
        [Command("join")]
        [Summary("Joins a lobby.")]
        public async Task Join() {
            var game = GetOrCreateGame(Context);
            await game.JoinGame(Context.Message.Author.Id);
        }
        
        [Command("start")]
        [Summary("Starts a game.")]
        public async Task Start() {
            var game = GetOrCreateGame(Context);
            await game.Start();
        }
    }
}