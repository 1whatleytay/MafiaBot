using System.Diagnostics;
using System.Threading.Tasks;

using Discord;
using Discord.WebSocket;

namespace MafiaBot {
    public class MafiaPlayer {
        public enum Role {
            Citizen,
            Mafia,
            Doctor,
            Investigator
        }

        private static Embed GetRoleEmbed(Role role) {
            switch (role) {
                case Role.Citizen:
                    return new EmbedBuilder()
                        .WithColor(Color.Green)
                        .WithTitle("You are a Citizen!")
                        .Build();
                case Role.Mafia:
                    return new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("You are part of the Mafia!")
                        .Build();
                case Role.Doctor:
                    return new EmbedBuilder()
                        .WithColor(Color.Blue)
                        .WithTitle("You are the Doctor!")
                        .Build();
                case Role.Investigator:
                    return new EmbedBuilder()
                        .WithColor(Color.Purple)
                        .WithTitle("You are the Investigator!")
                        .Build();
                default:
                    return new EmbedBuilder()
                        .WithTitle("You are some new role.")
                        .Build();
            }
        }

        private readonly DiscordSocketClient _client;
        private readonly ulong _userId;
        private Role _role = Role.Citizen;
        
        public async Task TellRole() {
            await (await GetDM()).SendMessageAsync("", false, GetRoleEmbed(_role));
        }
        
        public void AssignRole(Role role) {
            _role = role;
        }

        public Role GetRole() {
            return _role;
        }

        public SocketUser GetUser() {
            return _client.GetUser(_userId);
        }
        
        public ulong GetId() {
            return _userId;
        }

        public async Task<IMessageChannel> GetDM() {
            return await GetUser().GetOrCreateDMChannelAsync();
        }
        
        public MafiaPlayer(DiscordSocketClient client, ulong userId) {
            _client = client;
            _userId = userId;
        }
    }
}