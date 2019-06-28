using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;

using Discord;
using Discord.Rest;
using Discord.WebSocket;

using MafiaBot.Roles;

using Context = Discord.Commands.ModuleBase<Discord.Commands.SocketCommandContext>;

namespace MafiaBot {
    public class MafiaContext : MafiaPlayers {
        private const int DiscussionTime = 30000;
        private const int DefendTime = 20000;
        private const long CitizenVoteTime = 60000;
        private const long NightTime = 60000;
        private const long LastStandVoteTime = 20000;
        
        private static readonly string[] DeathMessages = File.ReadAllLines("Lines/messages.txt");
        
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
        
        private static string RandomDeathMessage(string name) {
            return string.Format(DeathMessages[Utils.Random.Next(DeathMessages.Length)], name);
        }
        
        private GameStatus _gameStatus = GameStatus.Closed;
        private Thread _gameThread;

        private List<MafiaPlayer> _voteOptions;
        private readonly Queue<MafiaVote> _voteQueue = new Queue<MafiaVote>();

        private readonly Dictionary<ulong, List<MafiaPlayer>> _selectOptions
            = new Dictionary<ulong, List<MafiaPlayer>>();
        private readonly Queue<MafiaVote> _selectQueue = new Queue<MafiaVote>();

        private RestUserMessage _lobbyMessage;

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
            if (votes.Count == 0) return null;
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

        // This is an enormous function that has most game logic.
        private async Task RunGame() {
            try {
                while (CheckGameWin() == WinReason.NoWinYet) {
                    // Night Time
                    await ChannelVisibility(GetGeneral(), Players, false, true);
                    await VoiceMute(Players, false);

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
                                var healedLast = doctorInfo.HealedLast();
                                _selectOptions[player.GetId()] = healedLast.HasValue
                                    ? Players.Where(x => x.GetId() != healedLast.Value).ToList()
                                    : new List<MafiaPlayer>(Players);

                                var dm = await player.GetDm();
                                await dm.SendMessageAsync(
                                    "Who do you want to save? Select someone with `-select <number>`:\n"
                                    + Utils.Code(BuildVoteOptions(_selectOptions[player.GetId()])));
                                break;
                            }
                            case MafiaPlayer.Role.Detective: {
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
                    while (stopwatch.ElapsedMilliseconds < NightTime
                           && (_selectOptions.Count > 0 || validVotes.Count < Players.Count(IsMafia))) {
                        // -vote
                        while (_voteQueue.Count > 0) {
                            var vote = _voteQueue.Dequeue();
                            
                            // Verify Vote
                            if (vote.Channel != GetMafia().Id) {
                                Console.WriteLine("Ignored non-#mafia vote.");
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
                                continue;
                            }
                            
                            var options = _selectOptions[vote.Voter];
                            
                            if (vote.Vote <= 0 || vote.Vote > options.Count) {
                                await dm.SendMessageAsync($"Please select a valid option (1 - {options.Count}).");
                                continue;
                            }

                            var target = options[vote.Vote - 1];

                            // After Selection
                            switch (player.GetRole()) {
                                case MafiaPlayer.Role.Doctor:
                                    doctorToSave.Add(target);
                                    break;
                                case MafiaPlayer.Role.Detective: {
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
                        await Kill(mafiaToKill, DeathReason.MafiaAttack);
                    
                    foreach (var silenced in silencerToSilence) {
                        var dm = await silenced.GetDm();
                        await dm.SendMessageAsync("You have been silenced! You aren't able to say anything!");
                    }

                    // Day Time
                    await ChannelVisibility(GetGeneral(), Players, x => !silencerToSilence.Contains(x), true);
                    await VoiceMute(Players, x => !silencerToSilence.Contains(x));
                    var embedTitle = "Uneventful night.";
                    var embedColor = Color.Blue;
                    var newsBuilder = new StringBuilder();
                    if (mafiaToKill == null) {
                        newsBuilder.Append("The mafia was asleep and didn't do anything.\n");
                    }  else {
                        var userInfo = mafiaToKill.GetUser();
                        embedTitle = $"**{userInfo.Username}** was killed!";
                        embedColor = Color.Red;
                        newsBuilder.Append(RandomDeathMessage(mafiaToKill.GetUser().Username) + "\n");
                        if (doctorToSave.Contains(mafiaToKill)) {
                            embedTitle = $"**{userInfo.Username}** was attacked and saved!";
                            embedColor = Color.Green;
                            newsBuilder.Append($"{mafiaToKill.GetUser().Username} was saved by a doctor!\n");
                        }
                    }
                    await SendGeneral(new EmbedBuilder()
                        .WithColor(embedColor)
                        .WithTitle(embedTitle)
                        .WithDescription(newsBuilder.ToString())
                        .Build());
                    
                    if (CheckGameWin() != WinReason.NoWinYet) break;
                    
                    Thread.Sleep(DiscussionTime);

                    var citizenToKill = await DoCitizenVote(silencerToSilence);

                    // Last Stand
                    if (citizenToKill != null) {
                        await SendGeneral($"<@{citizenToKill.GetId()}> **You're on your last stand. " +
                                          "You have 20 seconds to defend yourself.**");
                        await ChannelVisibility(GetGeneral(), Players,
                            x => x.GetId() == citizenToKill.GetId(), true);
                        await VoiceMute(Players, x => x.GetId() == citizenToKill.GetId());
                        Thread.Sleep(DefendTime);
                        
                        // Make the game think there is a vote with two options, sketchy solution for now.
                        await ChannelVisibility(GetGeneral(), Players, true);
                        await VoiceMute(Players, true);
                        _voteOptions = new List<MafiaPlayer>();
                        var options = Utils.Code(
                            "1. Innocent\n" +
                            "2. Guilty");
                        var tally = await SendGeneral("Guilty or innocent? Vote with `-vote <number>`." + options);

                        var innocent = new List<ulong>();
                        var guilty = new List<ulong>();
                        
                        stopwatch.Restart();
                        while (stopwatch.ElapsedMilliseconds < LastStandVoteTime) {
                            while (_voteQueue.Count > 0) {
                                var vote = _voteQueue.Dequeue();
                                
                                if (silencerToSilence.Exists(x => x.GetId() == vote.Voter)) {
                                    Console.WriteLine("Ignored silenced vote.");
                                    continue;
                                }
                
                                if (vote.Vote <= 0 || vote.Vote > 2) {
                                    await SendGeneral("Please select a valid option (1 - 2).");
                                    continue;
                                }

                                if (vote.Voter == citizenToKill.GetId()) {
                                    await SendGeneral("You can't vote for yourself, defendant.");
                                    continue;
                                }

                                if (innocent.Contains(vote.Voter)) innocent.Remove(vote.Voter);
                                if (guilty.Contains(vote.Voter)) guilty.Remove(vote.Voter);
                                
                                if (vote.Vote == 1) innocent.Add(vote.Voter);
                                if (vote.Vote == 2) guilty.Add(vote.Voter);
                                
                                await tally.ModifyAsync(
                                    x => x.Content = "Guilty or innocent? Vote with `-vote <number>`. " +
                                                     $"{innocent.Count} Innocent, {guilty.Count} Guilty" + options);
                            }
                        }
                        _voteOptions = null;

                        if (innocent.Count == guilty.Count) {
                            await SendGeneral("Citizens are not convinced! " +
                                              $"<@{citizenToKill.GetId()}> is acquitted. " +
                                              "Time for bed!");
                        } else {
                            await SendGeneral($"<@{citizenToKill.GetId()}> is found guilty. He/She is now dead. " +
                                              "Everyone goes to bed.");
                            await Kill(citizenToKill, DeathReason.VotedOut);
                        }
                    } else {
                        await SendGeneral("No one was put on trial. Time to go to bed.");
                    }
                }

                await SendGeneral("Game is over. " + WinReasonExplanation(CheckGameWin()));
                await Reset();
            } catch (Exception e) {
                await SendGeneral(Utils.Code("RunGame() Exception -> " + e.Message + "\n\n" + e.StackTrace));
            }
        }
        
        private async Task InitializeGame() {
            _gameStatus = GameStatus.InGame;
            await SendGeneral("Game is starting!");
            
            await AssignRoles();
            await ChannelVisibility(GetMafia(), false);
            await ChannelVisibility(GetMafia(), Players, x => x.GetRole() == MafiaPlayer.Role.Mafia);
            var mafiaNames = string.Join(" ",
                Players.Where(x => x.GetRole() == MafiaPlayer.Role.Mafia).Select(x => $"<@{x.GetId()}>"));
            await SendMafia($"**Welcome to the Mafia!** Your members are {mafiaNames}. Say hi.");

            _gameThread = new Thread(() => { RunGame().GetAwaiter().GetResult(); });
            _gameThread.Start();
        }

        private async Task RefreshLobby() {
            await _lobbyMessage.ModifyAsync(x => x.Embed = new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle($"Lobby ({Players.Count})")
                .WithDescription(
                    "Join with `-join`. " +
                    (Players.Count >= 4 ? "Start with `-start`." : "Cannot Start.") + "\n\n" +
                                 string.Join("\n", Players.Select(y => $"**{y.GetUser().Username}**")))
                .Build());
        }

        public async Task JoinGame(SocketUserMessage message) {
            var user = message.Author.Id;
            if (_gameStatus != GameStatus.Lobby) {
                await SendGeneral("Excited? Please wait until the next game starts.");
                return;
            }

            if (Players.Exists(x => x.GetId() == user)) {
                await SendGeneral("You're already part of this game! Wait a minute.");
                return;
            }
            
            Players.Add(new MafiaPlayer(Client, user));
            await RefreshLobby();
            // The Send/Delete approach here is used to prevent channel clutter but also give feedback to the user.
            await message.Channel.SendMessageAsync($"<@{user}> joined the lobby.");
            await message.DeleteAsync();
        }

        public async Task LeaveGame(SocketUserMessage message) {
            var user = message.Author.Id;
            if (_gameStatus != GameStatus.Lobby && _gameStatus != GameStatus.InGame) {
                await SendGeneral("There isn't any game to leave!");
                return;
            }

            if (!Players.Exists(x => x.GetId() == user)) {
                await SendGeneral("You aren't a part of this game.");
                return;
            }

            Players.RemoveAll(x => x.GetId() == user);
            await RefreshLobby();
            // Send/Delete approach explained above.
            await message.Channel.SendMessageAsync($"<@{user}> left the lobby.");
            await message.DeleteAsync();
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
                await SendGeneral($"<@{vote.Voter}> You aren't a part of this game. Wait until next time.");
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
            _lobbyMessage = await SendGeneral(new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithTitle("Empty Lobby")
                .WithDescription("Join with `-join`.")
                .Build());
        }

        public async Task Start() {
            if (_gameStatus != GameStatus.Lobby) {
                await SendGeneral("Please create a lobby or wait until the game is finished.");
                return;
            }

            if (Players.Count < 4) {
                await SendGeneral($"You can't play mafia with only {Players.Count} players. You need at least 4!");
                return;
            }

            await InitializeGame();
        }

        public async Task Reset() {
            await EveryoneOnlyVisibility(GetGeneral());
            await ChannelVisibility(GetGeneral(), true);
            await EveryoneOnlyVisibility(GetMafia());
            await ChannelVisibility(GetMafia(), true);
            await VoiceMute(true);

            foreach (var player in Killed)
            {
                await GetGuild().GetUser(player.GetId()).RemoveRoleAsync(GetDeadRole());
            }
            
            Players.Clear();
            Killed.Clear();
            _gameStatus = GameStatus.Closed;
        }

        public MafiaContext(DiscordSocketClient client, ulong guildId) : base(client, guildId) { }
    }
}