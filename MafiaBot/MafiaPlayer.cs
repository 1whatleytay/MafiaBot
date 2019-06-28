using System.IO;
using System.Threading.Tasks;

using Discord;
using Discord.WebSocket;

using MafiaBot.Roles;

namespace MafiaBot {
    public class MafiaPlayer {
        private static readonly string CitizenDescription = File.ReadAllText("Lines/Roles/citizen.txt");
        private static readonly string MafiaDescription = File.ReadAllText("Lines/Roles/mafia.txt");
        private static readonly string DoctorDescription = File.ReadAllText("Lines/Roles/doctor.txt");
        private static readonly string DetectiveDescription = File.ReadAllText("Lines/Roles/detective.txt");
        private static readonly string SilencerDescription = File.ReadAllText("Lines/Roles/silencer.txt");
        
        public enum Role {
            Citizen,
            Mafia,
            Doctor,
            Detective,
            Silencer
        }

        private static Embed GetRoleEmbed(Role role) {
            switch (role) {
                case Role.Citizen:
                    return new EmbedBuilder()
                        .WithColor(Color.Green)
                        .WithTitle("You are a Citizen!")
                        .WithDescription(CitizenDescription)
                        .WithImageUrl(
                            "https://raw.githubusercontent.com/1whatleytay/MafiaBot/master/MafiaBot/Images/citizen.png")
                        .Build();
                case Role.Mafia:
                    return new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("You are part of the Mafia!")
                        .WithDescription(MafiaDescription)
                        .WithImageUrl(
                            "https://raw.githubusercontent.com/1whatleytay/MafiaBot/master/MafiaBot/Images/mafia.png")
                        .Build();
                case Role.Doctor:
                    return new EmbedBuilder()
                        .WithColor(Color.Blue)
                        .WithTitle("You are the Doctor!")
                        .WithDescription(DoctorDescription)
                        .Build();
                case Role.Detective:
                    return new EmbedBuilder()
                        .WithColor(Color.Orange)
                        .WithTitle("You are the Detective!")
                        .WithDescription(DetectiveDescription)
                        .WithImageUrl(
                            "https://raw.githubusercontent.com/1whatleytay/MafiaBot/master/MafiaBot/Images/detective.png")
                        .Build();
                case Role.Silencer:
                    return new EmbedBuilder()
                        .WithColor(Color.DarkPurple)
                        .WithTitle("You are the Silencer!")
                        .WithDescription(SilencerDescription)
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