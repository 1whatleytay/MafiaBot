using System.IO;
using System.Threading.Tasks;

using Discord.Commands;

namespace Mafioso.Commands {
    public class Info : ModuleBase<SocketCommandContext> {
        private static readonly string HelpText = File.ReadAllText("Lines/help.txt");
        [Command("help")]
        [Summary("Help! I need to know the commands!")]
        public async Task Help() {
            await ReplyAsync(HelpText);
        }
    }
}