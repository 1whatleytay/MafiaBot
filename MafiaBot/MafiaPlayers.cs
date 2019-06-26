using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Discord.WebSocket;

namespace MafiaBot {
    public class MafiaPlayers : MafiaChannels {
        private const double MafiaPercentage = 1.0 / 5.0; // Rounded up
        private const double DoctorPercentage = 1.0 / 5.0; // Rounded down
        private const double InvestigatorPercentage = 1.0 / 7.0; // Rounded down
        
        protected readonly List<MafiaPlayer> Players = new List<MafiaPlayer>();
        protected readonly List<MafiaPlayer> Killed = new List<MafiaPlayer>();

        protected static bool IsGood(MafiaPlayer player) {
            return player.GetRole() != MafiaPlayer.Role.Mafia;
        }

        protected static bool IsMafia(MafiaPlayer player) {
            return player.GetRole() == MafiaPlayer.Role.Mafia;
        }
        
        protected async Task Kill(MafiaPlayer player) {
            Players.Remove(player);
            Killed.Add(player);

            await ChannelVisibility(GetGeneral(), Killed, x => false, true);
        }

        protected async Task AssignRoles() {
            var mafiaCount = Math.Ceiling(Players.Count * MafiaPercentage);
            var doctorCount = Math.Ceiling(Players.Count * DoctorPercentage);
            var investigatorCount = Math.Ceiling(Players.Count * InvestigatorPercentage);
            
            var pool = new List<MafiaPlayer>(Players);

            for (var a = 0; a < mafiaCount; a++) {
                var player = pool[Utils.Random.Next(pool.Count)];
                player.AssignRole(MafiaPlayer.Role.Mafia);
                pool.Remove(player);
            }
            
            for (var a = 0; a < doctorCount; a++) {
                var player = pool[Utils.Random.Next(pool.Count)];
                player.AssignRole(MafiaPlayer.Role.Doctor);
                pool.Remove(player);
            }
            
            for (var a = 0; a < investigatorCount; a++) {
                var player = pool[Utils.Random.Next(pool.Count)];
                player.AssignRole(MafiaPlayer.Role.Investigator);
                pool.Remove(player);
            }

            foreach (var player in Players) {
                await player.TellRole();
            }
        }

        protected MafiaPlayers(DiscordSocketClient client, ulong guildId) : base(client, guildId) { }
    }
}