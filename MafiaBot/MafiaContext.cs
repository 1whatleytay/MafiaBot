using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Discord;
using Discord.Rest;
using Discord.WebSocket;

using Context = Discord.Commands.ModuleBase<Discord.Commands.SocketCommandContext>;

namespace MafiaBot {
    public class MafiaContext : MafiaChannels {
        private const double MafiaPercentage = 0.20;
        
        private enum GameStatus {
            Lobby,
            InGame,
            Closed
        }

        private enum WinReason {
            Closed,
            MafiaIsHalf,
            EvilIsDead,
            NoWinYet,
        }
        
        private readonly List<MafiaPlayer> _players = new List<MafiaPlayer>();
        private GameStatus _gameStatus = GameStatus.Closed;
        private Thread _gameThread;

        private List<MafiaPlayer> _voteOptions;
        private readonly Queue<MafiaVote> _voteQueue = new Queue<MafiaVote>();
        
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
        
        private WinReason CheckGameWin() {
            var good = _players.Count(x => x.GetRole() != MafiaPlayer.Role.Mafia);
            var mafia = _players.Count(x => x.GetRole() == MafiaPlayer.Role.Mafia);

            if (mafia > good) return WinReason.MafiaIsHalf;
            if (mafia == 0) return WinReason.EvilIsDead;

            return WinReason.NoWinYet;
        }

        private string BuildVoteOptions(Func<MafiaPlayer, bool> filter) {
            var builder = new StringBuilder();
            
            _voteOptions = _players.Where(filter).ToList();

            for (var a = 0; a < _players.Count; a++) {
                if (!filter(_players[a])) continue;
                
                var user = _players[a].GetUser();
                builder.Append($"{(a + 1).ToString().PadLeft(2, ' ')}. {user.Username}");
            }
            
            return builder.ToString();
        }

        private async Task RunGame() {
            while (CheckGameWin() == WinReason.NoWinYet) {
                // Night Time
                await SendGeneral("Waiting for Mafia...");
                await SendMafia("Who do want to kill? Vote with `-select <number>`:\n"
                    + Utils.Code(BuildVoteOptions(x => x.GetRole() != MafiaPlayer.Role.Mafia)));
            }
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

        public void VoteFor(MafiaVote vote) {
            _voteQueue.Enqueue(vote);
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

        public MafiaContext(DiscordSocketClient client, ulong guildId) : base(client, guildId) { }
    }
}