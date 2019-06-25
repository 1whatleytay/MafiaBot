using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Discord.WebSocket;

using Context = Discord.Commands.ModuleBase<Discord.Commands.SocketCommandContext>;

namespace MafiaBot {
    public class MafiaContext : MafiaChannels {
        private const double MafiaPercentage = 0.20;

        private const long MafiaVoteTime = 60000;
        
        private enum GameStatus {
            Lobby,
            InGame,
            Closed
        }

        private enum WinReason {
            Closed,
            MafiaIsHalf,
            EvilIsDead,
            NoWinYet
        }

        private enum DeathReason {
            MafiaAttacked,
            VotedOut
        }
        
        private readonly List<MafiaPlayer> _players = new List<MafiaPlayer>();
        private readonly List<MafiaPlayer> _killed = new List<MafiaPlayer>();
        private GameStatus _gameStatus = GameStatus.Closed;
        private Thread _gameThread;

        private List<MafiaPlayer> _voteOptions;
        private readonly Queue<MafiaVote> _voteQueue = new Queue<MafiaVote>();

        private async Task Kill(MafiaPlayer player) {
            _players.Remove(player);
            _killed.Add(player);

            await ChannelVisibility(GetGeneral(), _killed, x => false, true);
        }
        
        private WinReason CheckGameWin() {
            if (_gameStatus == GameStatus.Closed) return WinReason.Closed;
            
            var good = _players.Count(x => x.GetRole() != MafiaPlayer.Role.Mafia);
            var mafia = _players.Count(x => x.GetRole() == MafiaPlayer.Role.Mafia);

            if (mafia > good) return WinReason.MafiaIsHalf;
            if (mafia == 0) return WinReason.EvilIsDead;

            return WinReason.NoWinYet;
        }

        private string BuildVoteOptions(Func<MafiaPlayer, bool> filter) {
            var builder = new StringBuilder();
            
            _voteOptions = _players.Where(filter).ToList();

            for (var a = 0; a < _voteOptions.Count; a++) {
                var user = _players[a].GetUser();
                builder.Append($"{(a + 1).ToString().PadLeft(2, ' ')}. {user.Username}\n");
            }
            
            return builder.ToString();
        }

        private async Task<MafiaPlayer> DoMafiaVote() {
            await SendGeneral("Waiting for Mafia...");
            await SendMafia("Who do want to kill? Vote with `-vote <number>`:\n"
                            + Utils.Code(BuildVoteOptions(x => x.GetRole() != MafiaPlayer.Role.Mafia)));

            var mafiaCount = _players.Count(x => x.GetRole() == MafiaPlayer.Role.Mafia);
            var validVotes = new List<MafiaVote>();
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            while (stopwatch.ElapsedMilliseconds < MafiaVoteTime
                   && validVotes.Count < mafiaCount) {
                while (_voteQueue.Count > 0) {
                    var vote = _voteQueue.Dequeue();
                        
                    if (vote.Channel != GetMafia().Id) {
                        await SendGeneral("Only the mafia can vote in their secret mafia channel.");
                        continue;
                    }

                    if (validVotes.Exists(x => x.Voter == vote.Voter)) {
                        validVotes.RemoveAll(x => x.Voter == vote.Voter);
                    }
                        
                    validVotes.Add(vote);
                    await SendMafia($"<@{vote.Voter}>! Your vote for #{vote.Vote} has been registered.");
                }
            }
            MafiaPlayer selected;
            if (validVotes.Count < 1)
                selected = null;
            else
                selected = _voteOptions[validVotes[Utils.Random.Next(validVotes.Count)].Vote];
            
            _voteOptions = null;
            await SendGeneral("Mafia is done voting.");

            return selected;
        }

        private async Task<MafiaPlayer> DoCitizenVote() {
            await SendGeneral("Anyone suspicious. Vote for someone to kill with `-vote <number>`:\n"
                              + Utils.Code(BuildVoteOptions(x => true)));

            return null;
        }

        private async Task RunGame() {
            while (CheckGameWin() == WinReason.NoWinYet) {
                // Night Time
                await ChannelVisibility(GetGeneral(), _players, x => false, true);
                var mafiaToKill = await DoMafiaVote();

                if (mafiaToKill != null)
                    await Kill(mafiaToKill);

                if (CheckGameWin() != WinReason.NoWinYet) break;
                
                // Day Time
                await ChannelVisibility(GetGeneral(), _players, x => true);
                var newsBuilder = new StringBuilder();
                if (mafiaToKill == null)
                    newsBuilder.Append("The mafia was asleep and didn't do anything.\n");
                else
                    newsBuilder.Append($"<@{mafiaToKill.GetId()}> was killed by the mafia last night.\n");
                await SendGeneral("Wake up everyone! Here's the rundown.\n" + newsBuilder + "Discuss!");
                
                Thread.Sleep(15000); // 15 seconds for discussion.
                
                var citizenToKill = await DoCitizenVote();
                if (citizenToKill == null) {
                    await SendGeneral("No one died! Time for bed.");
                } else {
                    await SendGeneral($"<@{citizenToKill.GetId()}> was killed. Now for the next day.");
                }
            }
        }

        private async Task AssignRoles() {
            var mafiaCount = Math.Ceiling(_players.Count * MafiaPercentage);
            var pool = new List<MafiaPlayer>(_players);

            for (var a = 0; a < mafiaCount; a++) {
                var player = pool[Utils.Random.Next(pool.Count)];
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
            await ChannelVisibility(GetMafia(), _players, x => x.GetRole() == MafiaPlayer.Role.Mafia);

            _gameThread = new Thread(() => { RunGame().GetAwaiter().GetResult(); });
            _gameThread.Start();
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
            
            _players.Add(new MafiaPlayer(Client, user));
            await SendGeneral($"<@{user}> joined! There are {_players.Count} players in the lobby.");
        }

        public async Task VoteFor(MafiaVote vote) {
            if (_gameStatus != GameStatus.InGame) {
                await SendGeneral("There isn't a game going on right now.");
                return;
            }

            if (_voteOptions == null) {
                await SendGeneral("There isn't a vote going on right now.");
                return;
            }

            if (vote.Vote <= 0 || vote.Vote >= _voteOptions.Count) {
                await SendGeneral($"Please select a valid option (1 - {_voteOptions.Count}).");
                return;
            }

            if (!_players.Exists(x => x.GetId() == vote.Voter)) {
                await SendGeneral("You aren't a part of this game. Wait until next time.");
                return;
            }

            if (_voteOptions.Exists(x => x.GetId() == vote.Voter)) {
                await SendGeneral("You can't vote for yourself.");
                return;
            }
            
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
            await ChannelVisibility(GetMafia(), _players, x => true);
            
            _players.Clear();
            _killed.Clear();
            _gameStatus = GameStatus.Closed;

            await SendGeneral("Game has been reset.");
        }

        public MafiaContext(DiscordSocketClient client, ulong guildId) : base(client, guildId) { }
    }
}