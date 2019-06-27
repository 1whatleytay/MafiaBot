using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;

using Discord.WebSocket;

using MafiaBot.Roles;

using Context = Discord.Commands.ModuleBase<Discord.Commands.SocketCommandContext>;

namespace MafiaBot {
    public class MafiaContext : MafiaPlayers {
        private const int DiscussionTime = 30000;
        private const long CitizenVoteTime = 90000;
        private const long NightTime = 60000;
        
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

        private Dictionary<ulong, List<MafiaPlayer>> _selectOptions = new Dictionary<ulong, List<MafiaPlayer>>();
        private readonly Queue<MafiaVote> _selectQueue = new Queue<MafiaVote>();

        private Dictionary<int, int> VoteScores(List<MafiaVote> votes) {
            var scores = new Dictionary<int, int>();
            
            foreach (var vote in votes) {
                if (!scores.ContainsKey(vote.Vote))
                    scores[vote.Vote] = 0;
                scores[vote.Vote]++;
            }

            return scores;
        }

        private int? HighestScore(List<MafiaVote> votes) {
            var scores = VoteScores(votes);
            var maxScore = scores.Max(x => x.Value);
            var top = scores.Where(x => x.Value == maxScore).ToList();
            if (top.Count == 1) return top[0].Key;
            return null;
        }
        
        private WinReason CheckGameWin() {
            if (_gameStatus == GameStatus.Closed) return WinReason.Closed;
            
            var notMafia = Players.Count(IsNotMafia);
            var mafia = Players.Count(IsMafia);

            if (mafia >= notMafia) return WinReason.MafiaIsHalf;
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

        private async Task<MafiaPlayer> DoCitizenVote(List<MafiaPlayer> cannotVote) {
            _voteOptions = new List<MafiaPlayer>(Players);
            await SendGeneral("Anyone suspicious? Vote for someone to kill with `-vote <number>`:\n"
                              + Utils.Code(BuildVoteOptions(_voteOptions)));

            var validVotes = new List<MafiaVote>();
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            while (stopwatch.ElapsedMilliseconds < CitizenVoteTime
                   && !(validVotes.Count > 0
                        && VoteScores(validVotes).Max(x => x.Value) >= Math.Ceiling(Players.Count / 2.0))) {
                while (_voteQueue.Count > 0) {
                    var vote = _voteQueue.Dequeue();
                    
                    if (vote.Channel != GetGeneral().Id) {
                        Console.WriteLine("Ignored non-#general vote during mafia vote.");
                        continue;
                    }

                    if (cannotVote.Exists(x => x.GetId() == vote.Voter)) {
                        Console.WriteLine("Ignored silenced vote.");
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
            
            MafiaPlayer selected = null;
            var top = HighestScore(validVotes);
            if (top.HasValue)
                selected = _voteOptions[top.Value - 1];

            _voteOptions = null;

            return selected;
        }

        private async Task RunGame() {
            try {
                while (CheckGameWin() == WinReason.NoWinYet) {
                    // Night Time
                    await ChannelVisibility(GetGeneral(), Players, false, true);

                    MafiaPlayer mafiaToKill = null;
                    var doctorToSave = new List<MafiaPlayer>();
                    var silencerToSilence = new List<MafiaPlayer>();
                    
                    // Role Setup
                    _voteOptions = Players.Where(IsNotMafia).ToList();
                    await SendMafia("Who do want to kill? Vote with `-vote <number>`:\n"
                                    + Utils.Code(BuildVoteOptions(_voteOptions)));

                    foreach (var player in Players) {
                        switch (player.GetRole()) {
                            case MafiaPlayer.Role.Doctor: {
                                var doctorInfo = player.GetInfo<DoctorRoleInfo>();
                                _selectOptions[player.GetId()] = doctorInfo.DidHealLast()
                                    ? Players.Where(x => x != player).ToList()
                                    : new List<MafiaPlayer>(Players);

                                var dm = await player.GetDm();
                                await dm.SendMessageAsync(
                                    "Who do you want to save? Select someone with `-select <number>`:\n"
                                    + Utils.Code(BuildVoteOptions(_selectOptions[player.GetId()])));
                                break;
                            }
                            case MafiaPlayer.Role.Investigator: {
                                _selectOptions[player.GetId()] = Players.Where(
                                    x => x.GetId() != player.GetId()).ToList();

                                var dm = await player.GetDm();
                                await dm.SendMessageAsync(
                                    "Who do you want to investigate? Select someone with `-select <number>`:\n"
                                    + Utils.Code(BuildVoteOptions(_selectOptions[player.GetId()])));
                                break;
                            }
                            case MafiaPlayer.Role.Silencer: {
                                _selectOptions[player.GetId()] = Players.Where(
                                    x => x.GetId() != player.GetId()).ToList();

                                var dm = await player.GetDm();
                                await dm.SendMessageAsync(
                                    "Who do you want to silence? Select someone with `-select <number>`:\n"
                                    + Utils.Code(BuildVoteOptions(_selectOptions[player.GetId()])));
                                break;
                            }
                        }
                    }
                    
                    // Wait Through Night Time
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();
                    var validVotes = new List<MafiaVote>();
                    while (stopwatch.ElapsedMilliseconds < NightTime) {
                        // -vote
                        while (_voteQueue.Count > 0) {
                            var vote = _voteQueue.Dequeue();
                            
                            // Verify Vote
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

                        // -select
                        while (_selectQueue.Count > 0) {
                            var vote = _selectQueue.Dequeue();

                            // Verify Selection
                            var player = Players.First(x => x.GetId() == vote.Voter);
                            var dm = await player.GetDm();
                            
                            if (!_selectOptions.ContainsKey(vote.Voter)) {
                                await dm.SendMessageAsync("Wait until its your turn!");
                            }
                            
                            var options = _selectOptions[vote.Voter];
                            
                            if (vote.Vote <= 0 || vote.Vote > _voteOptions.Count) {
                                await dm.SendMessageAsync($"Please select a valid option (1 - {_voteOptions.Count}).");
                                continue;
                            }

                            var target = options[vote.Vote - 1];

                            // After Selection
                            switch (player.GetRole()) {
                                case MafiaPlayer.Role.Doctor:
                                    doctorToSave.Add(target);
                                    break;
                                case MafiaPlayer.Role.Investigator: {
                                    var isGood = IsGood(target);
                                    await dm.SendMessageAsync("The person you investigated... "
                                                              + (isGood ? "seems normal." : "is suspicious."));
                                    break;
                                }
                                case MafiaPlayer.Role.Silencer:
                                    silencerToSilence.Add(target);
                                    break;
                                default:
                                    await dm.SendMessageAsync("You don't have anything to select from.");
                                    continue;
                            }
                            
                            _selectOptions.Remove(player.GetId());
                        }
                    }
                    
                    var top = HighestScore(validVotes);
                    if (top.HasValue)
                        mafiaToKill = _voteOptions[top.Value - 1];
                    _voteOptions = null;
                    _selectOptions.Clear();

                    if (mafiaToKill != null && !doctorToSave.Contains(mafiaToKill))
                        await Kill(mafiaToKill);
                    
                    foreach (var silenced in silencerToSilence) {
                        var dm = await silenced.GetDm();
                        await dm.SendMessageAsync("You have been silenced! You aren't able to say anything!");
                    }

                    if (CheckGameWin() != WinReason.NoWinYet) break;

                    // Day Time
                    await ChannelVisibility(GetGeneral(), Players, x => !silencerToSilence.Contains(x));
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

                    var citizenToKill = await DoCitizenVote(silencerToSilence);
                    
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
            var mafiaNames = string.Join(" ",
                Players.Where(x => x.GetRole() == MafiaPlayer.Role.Mafia).Select(x => $"<@{x.GetId()}>"));
            await SendMafia($"**Welcome to the Mafia!** Your members are {mafiaNames}. Say hi.");

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

            var dm = await player.GetDm();
            if (_gameStatus != GameStatus.InGame) {
                await dm.SendMessageAsync("Please wait until a game starts.");
                return;
            }

            if (_selectOptions == null) {
                await dm.SendMessageAsync("You can't select from anything yet. Please wait.");
                return;
            }

            _selectQueue.Enqueue(vote);
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