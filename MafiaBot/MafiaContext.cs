using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Discord.Rest;
using Discord.WebSocket;

using Context = Discord.Commands.ModuleBase<Discord.Commands.SocketCommandContext>;

namespace MafiaBot {
    public class MafiaContext {
        private const double MafiaPercentage = 0.22;
        
        private enum GameStatus {
            Lobby,
            InGame,
            Closed,
        }
        
        private readonly DiscordSocketClient _client;
        private readonly ulong _guildId;
        private readonly List<MafiaPlayer> _players = new List<MafiaPlayer>();
        private GameStatus _gameStatus = GameStatus.Closed;

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

        private void MuteGeneral() {
            // TODO: Implement for night time.
        }

        private async Task<RestUserMessage> SendGeneral(string text) {
            return await GetGeneral().SendMessageAsync(text);
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

            // Add Mafia to GetMafia()
            // Start thread for night/day cycle.
            // Do the rest.
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
                await SendGeneral("You cannot start a game without at least 3 players. Get some friends together :D");
                return;
            }

            _gameStatus = GameStatus.InGame;
            await InitializeGame();
        }

        public async Task Reset() {
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