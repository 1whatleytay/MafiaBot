using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Discord;
using Discord.Rest;
using Discord.WebSocket;

using Context = Discord.Commands.ModuleBase<Discord.Commands.SocketCommandContext>;

namespace MafiaBot {
    public class MafiaContext {
        private const double MafiaPercentage = 0.20;
        
        private enum GameStatus {
            Lobby,
            InGame,
            Closed
        }
        
        private readonly DiscordSocketClient _client;
        private readonly ulong _guildId;
        private readonly List<MafiaPlayer> _players = new List<MafiaPlayer>();
        private GameStatus _gameStatus = GameStatus.Closed;
        private Thread _gameThread;

        private SocketGuild GetGuild() {
            return _client.GetGuild(_guildId);
        }

        private SocketCategoryChannel GetCategory() {
            return GetGuild().CategoryChannels.First(x => x.Name == "Mafia");
        }

        private SocketTextChannel GetGeneral() {
            return GetCategory().Channels.First(x => x.Name == "general") as SocketTextChannel;
        }

        private SocketTextChannel GetMafia() {
            return GetCategory().Channels.First(x => x.Name == "mafia") as SocketTextChannel;
        }

        private async Task ChannelVisibility(SocketTextChannel channel, Func<MafiaPlayer, bool> filter) {
            foreach (var player in _players) {
                var permission = filter(player) ? PermValue.Allow : PermValue.Deny;
                await channel.AddPermissionOverwriteAsync(player.GetUser(),
                    new OverwritePermissions(
                        viewChannel: permission,
                        sendMessages: permission
                    ));
            }
        }

        private async Task<RestUserMessage> SendGeneral(string text) {
            return await GetGeneral().SendMessageAsync(text);
        }

        private async Task RunGame() {
            // Put Day/Night Cycle here
        }

        private async Task AssignRoles() {
            var mafiaCount = Math.Ceiling(_players.Count * MafiaPercentage);
            var pool = new List<MafiaPlayer>(_players);
            
            var random = new Random();

            for (var a = 0; a < mafiaCount; a++) {
                var player = pool[(int)(random.NextDouble() * pool.Count)];
                player.AssignRole(MafiaPlayer.Role.Mafia);
                pool.Remove(player);
            }

            foreach (var player in _players) {
                await player.TellRole();
            }
        }

        private async Task InitializeGame() {
            await SendGeneral("Game is starting!");
            
            await AssignRoles();
            await ChannelVisibility(GetMafia(), x => x.GetRole() == MafiaPlayer.Role.Mafia);

            _gameThread = new Thread(() => { RunGame().RunSynchronously(); });
        }

        public async Task JoinGame(ulong user) {
            if (_gameStatus != GameStatus.Lobby) {
                await SendGeneral("Excited? Please wait until the next game starts.");
                return;
            }

            if (_players.Exists(x => x.GetId() == user)) {
                await SendGeneral("You're already part of this game! Wait a minute.");
                return;
            }
            
            _players.Add(new MafiaPlayer(_client, user));
            await SendGeneral($"<@{user}> joined! There are {_players.Count} players in the lobby.");
        }

        public async Task Create() {
            if (_gameStatus != GameStatus.Closed) {
                await SendGeneral("Please wait until the current game finishes.");
                return;
            }
            
            _gameStatus = GameStatus.Lobby;
            await SendGeneral("Created Lobby.");
        }

        public async Task Start() {
            if (_gameStatus != GameStatus.Lobby) {
                await SendGeneral("Please create a lobby or wait until the game is finished.");
                return;
            }

            if (_players.Count < 3) {
                await SendGeneral($"You can't play mafia with only {_players.Count} players. You need at least 3!");
                return;
            }

            _gameStatus = GameStatus.InGame;
            await InitializeGame();
        }

        public async Task Reset() {
            await ChannelVisibility(GetMafia(), x => true);
            
            _players.Clear();
            _gameStatus = GameStatus.Closed;

            await SendGeneral("Game has been reset.");
        }

        public MafiaContext(DiscordSocketClient client, ulong guildId) {
            _client = client;
            _guildId = guildId;
        }
    }
}