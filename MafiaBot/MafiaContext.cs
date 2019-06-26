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
    public class MafiaContext : MafiaPlayers {
        private const int DiscussionTime = 30000;
        private const long MafiaVoteTime = 60000;
        private const long CitizenVoteTime = 90000;
        private const long SelectTime = 40000;
        
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

        private static string WinReasonExplanation(WinReason reason) {
            switch (reason) {
                case WinReason.Closed:
                    return "The game closed.";
                case WinReason.MafiaIsHalf:
                    return "Mafia wins! They have taken over the town.";
                case WinReason.EvilIsDead:
                    return "Citizens win! All the evil are dead.";
                case WinReason.NoWinYet:
                    return "No one wins. Too bad.";
                default:
                    return "I win.";
            }
        }
        
        private GameStatus _gameStatus = GameStatus.Closed;
        private Thread _gameThread;

        private List<MafiaPlayer> _voteOptions;
        private readonly Queue<MafiaVote> _voteQueue = new Queue<MafiaVote>();

        private List<MafiaPlayer> _selectOptions;
        private MafiaVote? _selection;

        private Dictionary<ulong, int> VoteScores(List<MafiaVote> votes) {
            var scores = new Dictionary<ulong, int>();
            
            foreach (var vote in votes) {
                scores[vote.Voter]++;
            }

            return scores;
        }
        
        private WinReason CheckGameWin() {
            if (_gameStatus == GameStatus.Closed) return WinReason.Closed;
            
            var good = Players.Count(IsGood);
            var mafia = Players.Count(IsMafia);

            if (mafia >= good) return WinReason.MafiaIsHalf;
            if (mafia == 0) return WinReason.EvilIsDead;

            return WinReason.NoWinYet;
        }

        private static string BuildVoteOptions(List<MafiaPlayer> options) {
            var builder = new StringBuilder();

            for (var a = 0; a < options.Count; a++) {
                var user = options[a].GetUser();
                builder.Append($"{(a + 1).ToString().PadLeft(2, ' ')}. {user.Username}\n");
            }
            
            return builder.ToString();
        }

        private async Task<MafiaPlayer> DoMafiaVote() {
            await SendGeneral("Waiting for Mafia...");
            _voteOptions = Players.Where(IsGood).ToList();
            await SendMafia("Who do want to kill? Vote with `-vote <number>`:\n"
                            + Utils.Code(BuildVoteOptions(_voteOptions)));

            var mafiaCount = Players.Count(IsMafia);
            var validVotes = new List<MafiaVote>();
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            while (stopwatch.ElapsedMilliseconds < MafiaVoteTime
                   && validVotes.Count < mafiaCount) {
                while (_voteQueue.Count > 0) {
                    var vote = _voteQueue.Dequeue();
                        
                    if (vote.Channel != GetMafia().Id) {
                        Console.WriteLine("Ignored non-#mafia vote during mafia vote.");
                        continue;
                    }
                    
                    if (vote.Vote <= 0 || vote.Vote > _voteOptions.Count) {
                        await SendMafia($"Please select a valid option (1 - {_voteOptions.Count}).");
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
            else {
                selected = _voteOptions[validVotes[Utils.Random.Next(validVotes.Count)].Vote - 1];
            }

            _voteOptions = null;

            return selected;
        }

        private async Task<MafiaPlayer> DoCitizenVote() {
            _voteOptions = new List<MafiaPlayer>(Players);
            await SendGeneral("Anyone suspicious? Vote for someone to kill with `-vote <number>`:\n"
                              + Utils.Code(BuildVoteOptions(_voteOptions)));

            var validVotes = new List<MafiaVote>();
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            while (stopwatch.ElapsedMilliseconds < CitizenVoteTime
                   && VoteScores(validVotes).Max(x => x.Value) < Math.Ceiling(Players.Count / 2.0)) {
                while (_voteQueue.Count > 0) {
                    var vote = _voteQueue.Dequeue();
                    
                    if (vote.Channel != GetGeneral().Id) {
                        Console.WriteLine("Ignored non-#general vote during mafia vote.");
                        continue;
                    }
                    
                    if (vote.Vote <= 0 || vote.Vote > _voteOptions.Count) {
                        await SendGeneral($"Please select a valid option (1 - {_voteOptions.Count}).");
                        continue;
                    }
                    
                    if (_voteOptions[vote.Vote - 1].GetId() == vote.Voter) {
                        await SendGeneral("You can't vote for yourself.");
                        continue;
                    }
                    
                    validVotes.Add(vote);
                    await SendGeneral($"<@{vote.Voter}>! Your vote for #{vote.Vote} has been registered.");
                }
            }
            
            MafiaPlayer selected;
            if (validVotes.Count < 1)
                selected = null;
            else
                selected = _voteOptions[validVotes[Utils.Random.Next(validVotes.Count)].Vote - 1];
            
            _voteOptions = null;

            return selected;
        }

        private async Task<MafiaPlayer> DoDoctorSelect(MafiaPlayer player) {
            await SendGeneral("Waiting for Doctor.");
            _selectOptions = Players.Where(x => x.GetId() != player.GetId()).ToList();
                
            var dm = await player.GetDM();
            await dm.SendMessageAsync("Who do you want to save? Select someone with `-select <number>`:\n"
                                      + Utils.Code(BuildVoteOptions(_selectOptions)));

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            while (stopwatch.ElapsedMilliseconds < SelectTime) {
                if (!_selection.HasValue) continue;
                    
                if (_selection.Value.Voter != player.GetId()) {
                    var wrong = await Players.First(x => x.GetId() == _selection.Value.Voter).GetDM();
                    await wrong.SendMessageAsync("Wait until its your turn to select something.");
                } else if (_selection.Value.Vote <= 0 || _selection.Value.Vote > _selectOptions.Count) {
                    await dm.SendMessageAsync($"Please select a valid option (1 - {_selectOptions.Count}).");
                } else {
                    break;
                }
                    
                _selection = null;
            }
            MafiaPlayer selection = null;
            if (_selection.HasValue)
                selection = _selectOptions[_selection.Value.Vote];
            
            _selectOptions = null;
            return selection;
        }

        private async Task RunGame() {
            try {
                while (CheckGameWin() == WinReason.NoWinYet) {
                    // Night Time
                    await ChannelVisibility(GetGeneral(), Players, false, true);
                    var mafiaToKill = await DoMafiaVote();
                    await SendGeneral("Mafia is done voting.");

                    var doctorToSave = new List<MafiaPlayer>();
                    foreach (var player in Players) {
                        if (player.GetRole() != MafiaPlayer.Role.Doctor) continue;

                        var save = await DoDoctorSelect(player);
                        if (save != null)
                            doctorToSave.Add(save);
                    }

                    if (mafiaToKill != null && !doctorToSave.Contains(mafiaToKill))
                        await Kill(mafiaToKill);

                    if (CheckGameWin() != WinReason.NoWinYet) break;

                    // Day Time
                    await ChannelVisibility(GetGeneral(), Players, true);
                    var newsBuilder = new StringBuilder();
                    if (mafiaToKill == null)
                        newsBuilder.Append("The mafia was asleep and didn't do anything.\n");
                    else {
                        newsBuilder.Append($"<@{mafiaToKill.GetId()}> was attacked by the mafia last night.\n");
                        if (doctorToSave.Contains(mafiaToKill))
                            newsBuilder.Append($"<@{mafiaToKill.GetId()}> was saved by a doctor!\n");
                    }
                    await SendGeneral("Wake up everyone! Here's the rundown.\n" + newsBuilder + "Discuss!");

                    Thread.Sleep(DiscussionTime);

                    var citizenToKill = await DoCitizenVote();
                    if (citizenToKill == null) {
                        await SendGeneral("No one died! Time for bed.");
                    } else {
                        await SendGeneral($"<@{citizenToKill.GetId()}> was killed. Now for the next day.");
                        await Kill(citizenToKill);
                    }
                }

                await SendGeneral("Game is over. " + WinReasonExplanation(CheckGameWin()));
            } catch (Exception e) {
                await SendGeneral(Utils.Code("RunGame() Exception -> " + e.Message + "\n\n" + e.StackTrace));
            }
        }

        private async Task InitializeGame() {
            await SendGeneral("Game is starting!");
            
            await AssignRoles();
            await ChannelVisibility(GetMafia(), Players, x => x.GetRole() == MafiaPlayer.Role.Mafia);

            _gameThread = new Thread(() => { RunGame().GetAwaiter().GetResult(); });
            _gameThread.Start();
        }

        public async Task JoinGame(ulong user) {
            if (_gameStatus != GameStatus.Lobby) {
                await SendGeneral("Excited? Please wait until the next game starts.");
                return;
            }

            if (Players.Exists(x => x.GetId() == user)) {
                await SendGeneral("You're already part of this game! Wait a minute.");
                return;
            }
            
            Players.Add(new MafiaPlayer(Client, user));
            await SendGeneral($"<@{user}> joined! There are {Players.Count} players in the lobby.");
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

            if (!Players.Exists(x => x.GetId() == vote.Voter)) {
                await SendGeneral($"<@{vote.Voter}>You aren't a part of this game. Wait until next time.");
                return;
            }
            
            _voteQueue.Enqueue(vote);
        }

        public async Task Select(MafiaVote vote) {
            var player = Players.FirstOrDefault(x => x.GetId() == vote.Voter);
            if (player == null) return;

            var dm = await player.GetDM();
            if (_gameStatus != GameStatus.InGame) {
                await dm.SendMessageAsync("Please wait until a game starts.");
                return;
            }

            if (_selectOptions == null) {
                await dm.SendMessageAsync("You can't select from anything yet. Please wait.");
                return;
            }

            _selection = vote;
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

            if (Players.Count < 3) {
                await SendGeneral($"You can't play mafia with only {Players.Count} players. You need at least 3!");
                return;
            }

            _gameStatus = GameStatus.InGame;
            await InitializeGame();
        }

        public async Task Reset() {
            await EveryoneOnlyVisibility(GetGeneral());
            await ChannelVisibility(GetGeneral(), true);
            await EveryoneOnlyVisibility(GetMafia());
            await ChannelVisibility(GetMafia(), true);
            
            Players.Clear();
            Killed.Clear();
            _gameStatus = GameStatus.Closed;

            await SendGeneral("Game has been reset.");
        }

        public MafiaContext(DiscordSocketClient client, ulong guildId) : base(client, guildId) { }
    }
}