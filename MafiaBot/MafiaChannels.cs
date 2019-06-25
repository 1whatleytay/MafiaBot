using System;
using System.Linq;
using System.Threading.Tasks;

using Discord;
using Discord.Rest;
using Discord.WebSocket;

namespace MafiaBot {
    public class MafiaChannels {
        protected readonly DiscordSocketClient _client;
        private readonly ulong _guildId;
        
        private SocketGuild GetGuild() {
            return _client.GetGuild(_guildId);
        }

        private SocketCategoryChannel GetCategory() {
            return GetGuild().CategoryChannels.FirstOrDefault(x => x.Name == "Mafia");
        }

        protected SocketTextChannel GetGeneral() {
            return GetCategory().Channels.FirstOrDefault(x => x.Name == "general") as SocketTextChannel;
        }

        protected SocketTextChannel GetMafia() {
            return GetCategory().Channels.FirstOrDefault(x => x.Name == "mafia") as SocketTextChannel;
        }

        public bool IsValidGameChannel(ulong channel) {
            return GetGeneral().Id == channel || GetMafia().Id == channel;
        }

        public bool IsSetup() {
            return GetCategory() != null && GetGeneral() != null && GetMafia() != null;
        }

        public async Task Setup() {
            var guild = GetGuild();
            
            ulong categoryId;
            
            ITextChannel general = null, mafia = null;
            if (guild.CategoryChannels.Any(x => x.Name == "Mafia")) {
                var category = guild.CategoryChannels.First(x => x.Name == "Mafia");
                categoryId = category.Id;
                general = category.Channels.FirstOrDefault(x => x.Name == "general"
                                                                && x is ITextChannel) as ITextChannel;
                mafia = category.Channels.FirstOrDefault(x => x.Name == "mafia"
                                                              && x is ITextChannel) as ITextChannel;
            } else {
                var category = await guild.CreateCategoryChannelAsync("Mafia");
                categoryId = category.Id;
            }

            if (general == null) {
                general = await GetGuild().CreateTextChannelAsync("general");
                await general.ModifyAsync(x => x.CategoryId = categoryId);
            }
            
            if (mafia == null) {
                mafia = await GetGuild().CreateTextChannelAsync("mafia");
                await mafia.ModifyAsync(x => x.CategoryId = categoryId);
            }
            
            await general.SendMessageAsync("Mafia is setup! Create a lobby with `-create`.");
        }

        protected async Task<RestUserMessage> SendGeneral(string text) {
            return await GetGeneral().SendMessageAsync(text);
        }

        protected async Task<RestUserMessage> SendMafia(string text) {
            return await GetMafia().SendMessageAsync(text);
        }

        protected MafiaChannels(DiscordSocketClient client, ulong guildId) {
            _client = client;
            _guildId = guildId;
        }
    }
}