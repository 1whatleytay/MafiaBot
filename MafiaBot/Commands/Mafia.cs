using System.Threading.Tasks;
using System.Collections.Generic;

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

        private async Task<bool> EnsureSetup(MafiaContext context) {
            if (!context.IsSetup()) {
                await ReplyAsync("Woah! I don't think we're set up yet. Set up with `-setup`.");
                return false;
            }

            return true;
        }

        [Command("setup")]
        [Summary("Gets all cozy and comfy in your server so you can start playing games!")]
        public async Task Setup() {
            var game = GetOrCreateGame(Context);
            if (game.IsSetup()) {
                await ReplyAsync("Mafia is already set up.");
                return;
            }
            
            await game.Setup();
        }
        
        [Command("create")]
        [Summary("Creates a game lobby.")]
        public async Task Create() {
            var game = GetOrCreateGame(Context);
            if (!await EnsureSetup(game)) return;
            await game.Create();
        }
        
        [Command("join")]
        [Summary("Joins a lobby.")]
        public async Task Join() {
            var game = GetOrCreateGame(Context);
            if (!await EnsureSetup(game)) return;
            await game.JoinGame(Context.Message.Author.Id);
        }
        
        [Command("start")]
        [Summary("Starts a game.")]
        public async Task Start() {
            var game = GetOrCreateGame(Context);
            if (!await EnsureSetup(game)) return;
            await game.Start();
        }
        
        [Command("reset")]
        [Summary("Prepares for the next game.")]
        public async Task Reset() {
            var game = GetOrCreateGame(Context);
            if (!await EnsureSetup(game)) return;

            await game.Reset();
        }

        [Command("vote")]
        [Summary("Vote for a specific person.")]
        public async Task Vote(int vote) {
            var game = GetOrCreateGame(Context);
            if (!await EnsureSetup(game)) return;
            
            if (!game.IsValidGameChannel(Context.Channel.Id)) {
                await ReplyAsync("Please, only vote in a game channel- under the \"Mafia\" category.");
                return;
            }
            
            await game.VoteFor(new MafiaVote(Context.Message, vote));
        }

        [Command("select")]
        [Summary("Select someone to perform an action on.")]
        public async Task Select(int vote) {
            var game = GetOrCreateGame(Context);
            if (!await EnsureSetup(game)) return;

            if (!game.IsValidGameChannel(Context.Channel.Id)) {
                await ReplyAsync("Please, only vote in a game channel- under the \"Mafia\" category.");
                return;
            }

            await game.Select(new MafiaVote(Context.Message, vote));
        }
    }
}