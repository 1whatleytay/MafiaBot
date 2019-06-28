using System.Linq;
using System.Threading.Tasks;

using Discord.Commands;

namespace Mafioso.Commands {
    public class Debug : ModuleBase<SocketCommandContext> {
        // Ping!
        [Command("hi")]
        public async Task Hello() {
            await ReplyAsync("Hello!");
        }

        [Command("probe")]
        public async Task ProbeRole([Remainder] string roleName) {
            var role = Context.Guild.Roles.FirstOrDefault(x => x.Name == roleName);
            if (role == null) {
                await ReplyAsync($"No role named {roleName}.");
                return;
            }

            await ReplyAsync($"{roleName}: {role.Id}");
        }
    }
}