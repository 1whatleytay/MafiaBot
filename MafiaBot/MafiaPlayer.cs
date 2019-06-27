using System.Threading.Tasks;

using Discord;
using Discord.WebSocket;

using MafiaBot.Roles;

namespace MafiaBot {
    public class MafiaPlayer {
        public enum Role {
            Citizen,
            Mafia,
            Doctor,
            Investigator,
            Silencer
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
                        .WithColor(Color.Orange)
                        .WithTitle("You are the Investigator!")
                        .Build();
                case Role.Silencer:
                    return new EmbedBuilder()
                        .WithColor(Color.DarkPurple)
                        .WithTitle("You are the Silencer!")
                        .Build();
                default:
                    return new EmbedBuilder()
                        .WithTitle("You are some new role.")
                        .Build();
            }
        }

        private readonly DiscordSocketClient _client;
        private readonly ulong _userId;
        private object _roleInfo;
        private Role _role = Role.Citizen;
        
        public async Task TellRole() {
            await (await GetDm()).SendMessageAsync("", false, GetRoleEmbed(_role));
        }
        
        public void AssignRole(Role role) {
            _role = role;

            switch (role) {
                case Role.Doctor:
                    _roleInfo = new DoctorRoleInfo();
                    break;
                default:
                    _roleInfo = null;
                    break;
            }
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

        public async Task<IMessageChannel> GetDm() {
            return await GetUser().GetOrCreateDMChannelAsync();
        }

        public T GetInfo<T>() where T : class {
            return _roleInfo as T;
        }
        
        public MafiaPlayer(DiscordSocketClient client, ulong userId) {
            _client = client;
            _userId = userId;
        }
    }
}