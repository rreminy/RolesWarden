using Discord;
using Discord.Interactions;
using RolesWarden.Models;
using RolesWarden.Services;
using RolesWarden.Utilities;
using System;
using System.Data;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace RolesWarden.Interactions
{
    [Group("config", "Warden Bot Configuration Commands")]
    [CommandContextType(InteractionContextType.Guild)]
    [RequireUserPermission(GuildPermission.ManageRoles)]
    public sealed class ConfigCommands : InteractionModuleBase
    {
        private GuildConfigurationService GuildConfigs { get; }
        private RoleConfigurationService RoleConfigs { get; }

        public ConfigCommands(GuildConfigurationService guildConfigs, RoleConfigurationService roleConfigs)
        {
            this.GuildConfigs = guildConfigs;
            this.RoleConfigs = roleConfigs;
        }

        [SlashCommand("show", "Show current configuration")]
        public async Task ShowAsync()
        {
            await this.DeferAsync();
            var guildConfig = await this.GuildConfigs.GetAsync(this.Context.Guild.Id);
            var roleConfigs = await (await this.RoleConfigs.GetGuildRolesAsync(this.Context.Guild.Id))
                .Where(config => config.Action is not RoleAction.Default)
                .ToAsyncEnumerable()
                .WhereAwait(async config => await this.Context.Guild.GetRoleAsync(config.RoleId) is not null)
                .ToListAsync();

            var embed = WardenUtils.CreateEmbed()
                .WithTitle($"Configuration for {this.Context.Guild.Name}");

            var action = guildConfig.DefaultAction;
            if (action is RoleAction.Default) action = RoleAction.Persist;
            embed.AddField("Default Action", action, true);
            embed.AddField("Ignore Admin Roles", guildConfig.IgnoreDangerous, true);

            var logText = "Not configured";
            if (guildConfig.LogChannelId is not 0)
            {
                var channel = await this.Context.Guild.GetTextChannelAsync(guildConfig.LogChannelId);
                if (channel is not null && await SavedRolesService.CheckPermissionsAsync(channel))
                {
                    logText = $"✅ {channel.Mention}";
                }
                else
                {
                    logText = $"❌ {channel?.Mention}";
                }
            }
            embed.AddField("Log Channel", logText, true);
            embed.AddField("Log Types", guildConfig.LogTypes, true);

            var configRolesText = "All defaults";
            if (roleConfigs.Count > 0) configRolesText = string.Join('\n', roleConfigs.Select(config => $"<@&{config.RoleId}> - {config.Action}"));
            embed.AddField("Configured Roles", configRolesText, false);

            await this.ModifyOriginalResponseAsync(message => message.Embed = embed.Build());
        }

        [SlashCommand("log", "Sets the log channel")]
        public async Task LogAsync([Summary("channel", "Channel to send logs to")] ITextChannel channel)
        {
            var embed = WardenUtils.CreateEmbed();
            if (channel.GuildId != this.Context.Guild.Id)
            {
                embed.Color = Color.Red;
                embed.Title = "Setting Log Channel Error";
                embed.Description = "Channel must be on this server";

                await this.RespondAsync(ephemeral: true, embed: embed.Build());
                return;
            }

            await this.DeferAsync();

            var guildConfig = await this.GuildConfigs.GetAsync(this.Context.Guild.Id);
            guildConfig.LogChannelId = channel.Id;
            await this.GuildConfigs.SetAsync(guildConfig);

            embed.Color = Color.Green;
            embed.Description = $"Log channel is now set to {channel.Mention}";

            if (!await SavedRolesService.CheckPermissionsAsync(channel))
            {
                embed.AddField("Warning", $"Bot does not have the required permissions at {channel.Mention}\n - Send Messages\n - Embed Links", false);
            }
            await this.ModifyOriginalResponseAsync(message => message.Embed = embed.Build());
        }

        [SlashCommand("remove_log", "Remove the log channel")]
        public async Task RemoveLogAsync()
        {
            await this.DeferAsync();
            var guildConfig = await this.GuildConfigs.GetAsync(this.Context.Guild.Id);
            guildConfig.LogChannelId = 0;
            await this.GuildConfigs.SetAsync(guildConfig);

            var embed = WardenUtils.CreateEmbed();
            embed.Color = Color.Green;
            embed.Description = "Log channel is now removed";
            await this.ModifyOriginalResponseAsync(message => message.Embed = embed.Build());
        }

        [SlashCommand("log_saved", "Configured whether to log saved user roles")]
        public async Task LogSavedAsync([Summary("enable", "Enable saved logs")] bool enable)
        {
            await this.DeferAsync();
            var guildConfig = await this.GuildConfigs.GetAsync(this.Context.Guild.Id);
            if (enable) guildConfig.LogTypes |= GuildLogType.Saved;
            else guildConfig.LogTypes &= ~GuildLogType.Saved;
            await this.GuildConfigs.SetAsync(guildConfig);

            var embed = WardenUtils.CreateEmbed();
            embed.Color = Color.Green;
            embed.Description = enable ? "Saved roles will now be logged" : "Saved roles will no longer be logged";
            await this.ModifyOriginalResponseAsync(message => message.Embed = embed.Build());
        }

        [SlashCommand("log_restored", "Configured whether to log restored user roles")]
        public async Task LogRestoredAsync([Summary("enable", "Enable restored logs")] bool enable)
        {
            await this.DeferAsync();
            var guildConfig = await this.GuildConfigs.GetAsync(this.Context.Guild.Id);
            if (enable) guildConfig.LogTypes |= GuildLogType.Restored;
            else guildConfig.LogTypes &= ~GuildLogType.Restored;
            await this.GuildConfigs.SetAsync(guildConfig);

            var embed = WardenUtils.CreateEmbed();
            embed.Color = Color.Green;
            embed.Description = enable ? "Restored roles will now be logged" : "Restored roles will no longer be logged";
            await this.ModifyOriginalResponseAsync(message => message.Embed = embed.Build());
        }

        [SlashCommand("default_action", "Configure default action to take for unconfigured roles")]
        public async Task DefaultActionAsync([Summary("action", "Default action to take. \"Default\" is \"Persist\".")] RoleAction action)
        {
            await this.DeferAsync();
            var config = await this.GuildConfigs.GetAsync(this.Context.Guild.Id);
            config.DefaultAction = action;
            await this.GuildConfigs.SetAsync(config);

            // Default is persist... although stored as default,
            // show the actual effective default to the user.
            if (action is RoleAction.Default) action = RoleAction.Persist;

            var embed = WardenUtils.CreateEmbed();
            embed.Color = Color.Green;
            embed.Description = $"Default action is now set to **{action}**";
            await this.ModifyOriginalResponseAsync(message => message.Embed = embed.Build());
        }

        [SlashCommand("ignore_dangerous_roles", "Configure ignore dangerous role mode")]
        public async Task IgnoreDangerousRolesAsync(IgnoreMode mode)
        {
            await this.DeferAsync();
            var config = await this.GuildConfigs.GetAsync(this.Context.Guild.Id);
            config.IgnoreDangerous = mode;
            await this.GuildConfigs.SetAsync(config);

            var embed = WardenUtils.CreateEmbed();
            embed.Color = Color.Green;
            embed.Description = $"Ignore admin roles is now set to **{mode}**";
            await this.ModifyOriginalResponseAsync(message => message.Embed = embed.Build());
        }

        [SlashCommand("role", "Configure an action for a role")]
        public async Task RoleAsync([Summary("role", "Role to configure")] IRole role, [Summary("action", "Whether to persist or ignore the role from being saved and restored")] RoleAction action)
        {
            var embed = WardenUtils.CreateEmbed();
            if (role.Guild.Id != this.Context.Guild.Id)
            {
                embed.Color = Color.Red;
                embed.Title = "Setting Role Error";
                embed.Description = "Role must be on this server";

                await this.RespondAsync(ephemeral: true, embed: embed.Build());
                return;
            }

            await this.DeferAsync();
            await this.RoleConfigs.SetAsync(role, action);

            embed.Color = Color.Green;
            embed.Description = $"Role {role.Mention} is now set to **{action}**";
            await this.ModifyOriginalResponseAsync(message => message.Embed = embed.Build());
        }

        [SlashCommand("reset_roles", "Reset configured roles to default")]
        public async Task ResetRolesAsync()
        {
            await this.DeferAsync();
            await this.RoleConfigs.ClearGuildAsync(this.Context.Guild.Id);

            var embed = WardenUtils.CreateEmbed();
            embed.Color = Color.Green;
            embed.Description = "All configured roles reset to default";
            await this.ModifyOriginalResponseAsync(message => message.Embed = embed.Build());
        }
    }
}
