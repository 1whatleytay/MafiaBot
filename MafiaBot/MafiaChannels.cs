using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Discord;
using Discord.Rest;
using Discord.WebSocket;

namespace MafiaBot {
    public class MafiaChannels {
        protected readonly DiscordSocketClient Client;
        private readonly ulong _guildId;
        
        protected SocketGuild GetGuild() {
            return Client.GetGuild(_guildId);
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

        protected SocketTextChannel GetDead()
        {
            return GetCategory().Channels.FirstOrDefault(x => x.Name == "dead") as SocketTextChannel;
        }

        protected SocketVoiceChannel GetVc() {
            return GetCategory().Channels.FirstOrDefault(x => x.Name == "Voice") as SocketVoiceChannel;
        }

        protected SocketRole GetDeadRole()
        {
            return GetGuild().Roles.FirstOrDefault(x => x.Name == "Dead");
        }
        
        // Copy of Visibility/Mute functions for convenience.
        protected async Task ChannelVisibility(SocketTextChannel channel, List<MafiaPlayer> players,
            Func<MafiaPlayer, bool> filter, bool onlyReceiving = false) {
            foreach (var player in players) {
                var permission = filter(player) ? PermValue.Allow : PermValue.Deny;
                await channel.AddPermissionOverwriteAsync(player.GetUser(),
                    new OverwritePermissions(
                        viewChannel: onlyReceiving ? PermValue.Allow : permission,
                        sendMessages: permission
                    ));
            }
        }
        
        protected async Task ChannelVisibility(SocketTextChannel channel, List<MafiaPlayer> players,
            bool visible, bool onlyReceiving = false) {
            var permission = visible ? PermValue.Allow : PermValue.Deny;
            
            foreach (var player in players) {
                await channel.AddPermissionOverwriteAsync(player.GetUser(),
                    new OverwritePermissions(
                        viewChannel: onlyReceiving ? PermValue.Allow : permission,
                        sendMessages: permission
                    ));
            }
        }

        protected async Task ChannelVisibility(SocketTextChannel channel, bool visible, bool onlyReceiving = false) {
            var permission = visible ? PermValue.Allow : PermValue.Deny;
            
            await channel.AddPermissionOverwriteAsync(GetGuild().EveryoneRole,
                new OverwritePermissions(
                    viewChannel: onlyReceiving ? PermValue.Allow : permission,
                    sendMessages: permission
                ));
        }

        protected async Task ChannelAllowBot(SocketTextChannel channel) {
            await channel.AddPermissionOverwriteAsync(Client.CurrentUser,
                new OverwritePermissions(
                    viewChannel: PermValue.Allow,
                    sendMessages: PermValue.Allow
                ));
        }

        protected async Task EveryoneOnlyVisibility(SocketTextChannel channel) {
            foreach (var permissionOverwrite in channel.PermissionOverwrites) {
                if (permissionOverwrite.TargetType == PermissionTarget.User) {
                    await channel.RemovePermissionOverwriteAsync(GetGuild().GetUser(permissionOverwrite.TargetId));
                }
            }
        }

        protected async Task VoiceMute(List<MafiaPlayer> players, Func<MafiaPlayer, bool> filter) {
            var vc = GetVc();
            foreach (var user in vc.Users) {
                var player = players.FirstOrDefault(x => x.GetId() == user.Id);
                if (player == null) continue;
                
                await user.ModifyAsync(x => x.Mute = !filter(player));
            }
        }

        protected async Task VoiceMute(List<MafiaPlayer> players, bool speakable) {
            var vc = GetVc();
            foreach (var user in vc.Users) {
                var player = players.FirstOrDefault(x => x.GetId() == user.Id);
                if (player == null) continue;
                
                await user.ModifyAsync(x => x.Mute = !speakable);
            }
        }

        protected async Task VoiceMute(bool speakable) {
            var vc = GetVc();
            foreach (var user in vc.Users) {
                await user.ModifyAsync(x => x.Mute = !speakable);
            }
        }
        
        public bool IsValidGameChannel(ulong channel) {
            return GetGeneral().Id == channel || GetMafia().Id == channel;
        }

        public bool IsSetup() {
            return GetCategory() != null && GetGeneral() != null && GetMafia() != null && GetDead() != null && GetVc() != null && GetDeadRole() != null;
        }

        public async Task Setup() {
            var guild = GetGuild();
            
            ulong categoryId;

            IRole deadRole = null;
            ITextChannel general = null, mafia = null, dead = null;
            IVoiceChannel vc = null;
            if (guild.CategoryChannels.Any(x => x.Name == "Mafia")) {
                var category = guild.CategoryChannels.First(x => x.Name == "Mafia");
                categoryId = category.Id;
                general = category.Channels.FirstOrDefault(x => x.Name == "general"
                                                                && x is ITextChannel) as ITextChannel;
                mafia = category.Channels.FirstOrDefault(x => x.Name == "mafia"
                                                              && x is ITextChannel) as ITextChannel;
                dead = category.Channels.FirstOrDefault(x => x.Name == "dead"
                                                             && x is ITextChannel) as ITextChannel;
                vc = category.Channels.FirstOrDefault(x => x.Name == "Voice"
                                                           && x is IVoiceChannel) as IVoiceChannel;
                deadRole = guild.Roles.FirstOrDefault(x => x.Name == "Dead");
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

            if (dead == null)
            {
                dead = await GetGuild().CreateTextChannelAsync("dead");
                await dead.ModifyAsync(x => x.CategoryId = categoryId);
            }

            if (vc == null) {
                vc = await GetGuild().CreateVoiceChannelAsync("Voice");
                await vc.ModifyAsync(x => x.CategoryId = categoryId);
            }

            if (deadRole == null)
            {
                deadRole = await GetGuild().CreateRoleAsync("Dead");
                await deadRole.ModifyAsync(x => x.Position = 0);
                await deadRole.ModifyAsync(x => x.Hoist = true);
            }
            
            await general.SendMessageAsync("Mafia is setup! Create a lobby with `-create`.");
        }

        protected async Task<RestUserMessage> SendGeneral(string text) {
            return await GetGeneral().SendMessageAsync(text);
        }

        protected async Task<RestUserMessage> SendGeneral(Embed embed) {
            return await GetGeneral().SendMessageAsync("", false, embed);
        }

        protected async Task<RestUserMessage> SendMafia(string text) {
            return await GetMafia().SendMessageAsync(text);
        }

        protected async Task<RestUserMessage> SendMafia(Embed embed) {
            return await GetMafia().SendMessageAsync("", false, embed);
        }

        protected MafiaChannels(DiscordSocketClient client, ulong guildId) {
            Client = client;
            _guildId = guildId;
        }
    }
}