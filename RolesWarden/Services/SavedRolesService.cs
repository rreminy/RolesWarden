using Discord;
using Discord.WebSocket;
using DryIocAttributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RolesWarden.Db;
using RolesWarden.Models;
using RolesWarden.Utilities;
using System;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace RolesWarden.Services
{
    [ExportMany]
    [SingletonReuse]
    public sealed partial class SavedRolesService : IHostedService
    {
        private readonly HashSet<ulong> _restoringUsers = [];
        
        private IDbContextFactory<WardenDbContext> DbPool { get; }
        private DiscordSocketClient Discord { get; }
        private RoleConfigurationService RoleConfigs { get; }
        private GuildConfigurationService GuildConfigs { get; }
        private ILogger Logger { get; }

        public SavedRolesService(IDbContextFactory<WardenDbContext> dbPool, DiscordSocketClient discord, RoleConfigurationService roleConfigs, GuildConfigurationService guildConfigs, ILogger<SavedRolesService> logger)
        {
            CommonLog.LogConstructing(logger, this);
            this.DbPool = dbPool;
            this.Discord = discord;
            this.RoleConfigs = roleConfigs;
            this.GuildConfigs = guildConfigs;
            this.Logger = logger;
            CommonLog.LogConstructed(logger, this);
        }

        private async Task Discord_ResetGuild(SocketGuild guild)
        {
            await this.ClearGuildAsync(guild);
        }

        public async Task<SavedRoles> GetAsync(ulong guildId, ulong userId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = await this.DbPool.CreateDbContextAsync(cancellationToken);
            return await GetCoreAsync(dbContext, guildId, userId, cancellationToken);
        }

        public async Task<SavedRoles> GetAsync(IGuildUser user, CancellationToken cancellationToken = default)
        {
            return await this.GetAsync(user.GuildId, user.Id, cancellationToken);
        }

        public async Task SetAsync(SavedRoles roles, CancellationToken cancellationToken = default)
        {
            await using var dbContext = await this.DbPool.CreateDbContextAsync(cancellationToken);
            await SetCoreAsync(dbContext, roles, cancellationToken);
        }

        public async Task SetAsync(ulong guildId, ulong userId, IEnumerable<ulong> roles, CancellationToken cancellationToken = default)
        {
            await this.SetAsync(new() { GuildId = guildId, UserId = userId, RoleIds = roles }, cancellationToken);
        }

        public async Task<bool> RemoveAsync(SavedRoles roles, CancellationToken cancellationToken = default)
        {
            await using var dbContext = await this.DbPool.CreateDbContextAsync(cancellationToken);
            return await RemoveCoreAsync(dbContext, roles, cancellationToken);
        }

        public async Task<bool> RemoveAsync(ulong guildId, ulong userId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = await this.DbPool.CreateDbContextAsync(cancellationToken);
            return await RemoveCoreAsync(dbContext, guildId, userId, cancellationToken);
        }

        public async Task ClearGuildAsync(ulong guildId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = await this.DbPool.CreateDbContextAsync(cancellationToken);
            await ClearGuildCoreAsync(dbContext, guildId, cancellationToken);
        }

        public async Task ClearGuildAsync(IGuild guild, CancellationToken cancellationToken = default)
        {
            await this.ClearGuildAsync(guild.Id, cancellationToken);
        }

        public static async Task<SavedRoles> GetCoreAsync(WardenDbContext dbContext, ulong guildId, ulong userId, CancellationToken cancellationToken = default)
        {
            return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
            {
                return (await dbContext.SavedRoles.FindAsync([guildId, userId], ct)) ?? new() { GuildId = guildId, UserId = userId };
            }, cancellationToken);
        }

        public static Task<SavedRoles> GetCoreAsync(WardenDbContext dbContext, IGuildUser user, CancellationToken cancellationToken = default)
        {
            return GetCoreAsync(dbContext, user.GuildId, user.Id, cancellationToken);
        }

        public static async Task SetCoreAsync(WardenDbContext dbContext, SavedRoles roles, CancellationToken cancellationToken = default)
        {
            roles.Timestamp = TimeService.Now;
            if (roles.RoleIds is not null && roles.RoleIds is not IList<ulong>) roles.RoleIds = roles.RoleIds.ToList();
            await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(roles, async (roles, ct) =>
            {
                if (await dbContext.SavedRoles.ContainsAsync(roles, ct))
                {
                    var entry = dbContext.SavedRoles.Attach(roles);
                    entry.State = EntityState.Modified;
                }
                else
                {
                    await dbContext.SavedRoles.AddAsync(roles, ct);
                }
                await dbContext.SaveChangesAsync(ct);
            }, cancellationToken);
        }

        public static async Task<bool> RemoveCoreAsync(WardenDbContext dbContext, SavedRoles roles, CancellationToken cancellationToken = default)
        {
            return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(roles, async (roles, ct) =>
            {
                dbContext.SavedRoles.Remove(roles);
                return await dbContext.SaveChangesAsync(ct) > 0;
            }, cancellationToken);
        }

        public static async Task<bool> RemoveCoreAsync(WardenDbContext dbContext, ulong guildId, ulong userId, CancellationToken cancellationToken = default)
        {
            return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
            {
                return await dbContext.SavedRoles.Where(roles => roles.GuildId == guildId && roles.UserId == userId).ExecuteDeleteAsync(ct) > 0;
            }, cancellationToken);
        }

        public static async Task<int> ClearGuildCoreAsync(WardenDbContext dbContext, ulong guildId, CancellationToken cancellationToken = default)
        {
            return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(guildId, async (guildId, ct) =>
            {
                return await dbContext.SavedRoles.Where(roles => roles.GuildId == guildId).ExecuteDeleteAsync(ct);
            }, cancellationToken);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            CommonLog.LogStarting(this.Logger, this);
            this.Discord.LeftGuild += this.Discord_ResetGuild;
            this.Discord.JoinedGuild += this.Discord_ResetGuild;
            this.Discord.UserJoined += this.RestoreRoles;
            this.Discord.GuildMemberUpdated += Discord_GuildMemberUpdated;
            this.Discord.UserLeft += Discord_UserLeft;
            CommonLog.LogStarted(this.Logger, this);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            CommonLog.LogStopping(this.Logger, this);
            this.Discord.LeftGuild -= this.Discord_ResetGuild;
            this.Discord.JoinedGuild -= this.Discord_ResetGuild;
            this.Discord.UserJoined -= this.RestoreRoles;
            this.Discord.GuildMemberUpdated -= Discord_GuildMemberUpdated;
            CommonLog.LogStopped(this.Logger, this);
            return Task.CompletedTask;
        }

        private Task Discord_GuildMemberUpdated(Cacheable<SocketGuildUser, ulong> old, SocketGuildUser user)
        {
            // Avoid saving roles if no changes are detected
            if (old.HasValue)
            {
                var oldRoles = old.Value.Roles.Select(role => role.Id).ToHashSet();
                var newRoles = user.Roles.Select(role => role.Id).ToHashSet();
                if (oldRoles.SetEquals(newRoles)) return Task.CompletedTask;
            }
            return this.SaveRoles(user);
        }

        private async Task Discord_UserLeft(SocketGuild guild, SocketUser user)
        {
            this.Logger.LogInformation("User {user} ({userId}) left {guild} ({guildId})", user.GlobalName, user.Id, guild.Name, guild.Id);

            var guildConfig = await this.GuildConfigs.GetAsync(guild);
            if (!guildConfig.LogTypes.HasFlag(GuildLogType.Saved)) return;

            var channelId = guildConfig.LogChannelId;
            if (channelId is 0) return;
            
            var channel = guild.GetTextChannel(channelId);
            if (channel is null || !await CheckPermissionsAsync(channel)) return;

            var savedRoles = await this.GetAsync(guild.Id, user.Id);
            var embed = CreateSavedEmbed(savedRoles);
            await channel.SendMessageAsync(embed: embed.Build());
        }

        private async Task RestoreRoles(SocketGuildUser user)
        {
            var userId = user.Id;
            var guild = user.Guild;
            var guildId = guild.Id;
            this.Logger.LogInformation("User {user} ({userId}) joined {guild} ({guildId}), restoring roles", user.DisplayName, userId, guild.Name, guildId);

            lock (this._restoringUsers)
            {
                if (!this._restoringUsers.Add(userId)) return;
            }
            try
            {
                var roles = await this.GetAsync(user);
                var guildConfig = await this.GuildConfigs.GetAsync(guildId);
                var roleConfigs = await this.RoleConfigs.GetAsync(roles.RoleIds ?? [], guildId);

                Debug.Assert(roleConfigs.All(config => config.GuildId == guildId));
                var botUser = guild.CurrentUser;
                var success = new List<ulong>();
                var failed = new List<KeyValuePair<ulong, Exception>>();
                foreach (var roleConfig in roleConfigs)
                {
                    try
                    {
                        // Ensure the role is to be persisted
                        var action = await GetRoleActionCoreAsync(guildConfig, roleConfig);
                        if (action is not RoleAction.Persist) continue;

                        // Ensure role exists and is not managed
                        var guildRole = await user.Guild.GetRoleAsync(roleConfig.RoleId);
                        if (guildRole is null || guildRole.IsManaged) continue;

                        // Ensure bot has the permission to give the role
                        var guildPermissions = botUser.GuildPermissions;
                        if (!guildPermissions.ManageRoles) continue;

                        // Ensure the role position is below the bot's highest role.
                        // NOTE: Highest role doesn't need the permission.
                        var highestPosition = botUser.Roles.Where(role => role.Id != guildId).Select(role => role.Position).Max();
                        var rolePosition = guildRole.Position;
                        if (rolePosition >= highestPosition) continue;

                        // Attempt giving the role to the user and record success.
                        var roleId = roleConfig.RoleId;
                        await user.AddRoleAsync(roleId);
                        success.Add(roleId);
                    }
                    catch (Exception ex)
                    {
                        // Record the failure to give the role to the user.
                        failed.Add(KeyValuePair.Create(roleConfig.RoleId, ex));

                        // The above means this should not happen, log this for later debugging.
                        this.Logger.LogError(ex, "Failed to give {roleId} to {user} ({userId}) at {guild} ({guildId})", roleConfig.RoleId, user.DisplayName, userId, guild.Name, guildId);
                    }
                }

                // Log feedback
                this.Logger.LogInformation("User {user} ({userId}) at {guild} ({guildId}) roles restored (Success: {success} | Failed: {failed})", user.DisplayName, userId, guild.Name, guildId, success.Count, failed.Count);

                if (!guildConfig.LogTypes.HasFlag(GuildLogType.Restored)) return;

                var channelId = guildConfig.LogChannelId;
                if (channelId is 0) return;

                var channel = guild.GetTextChannel(channelId);
                if (channel is null || !await CheckPermissionsAsync(channel)) return;

                if (success.Count + failed.Count > 0)
                {
                    var embed = CreateRestoredEmbed(userId, success, failed);
                    await channel.SendMessageAsync(embed: embed.Build());
                }
            }
            finally
            {
                lock (this._restoringUsers) this._restoringUsers.Remove(userId);
            }
            await this.SaveRoles(user);
        }

        private async Task SaveRoles(SocketGuildUser user)
        {
            var userId = user.Id;
            var guild = user.Guild;
            var guildId = guild.Id;
            lock (this._restoringUsers)
            {
                // Avoid users being restored
                if (this._restoringUsers.Contains(userId)) return;
            }
            this.Logger.LogInformation("User {user} ({userId}) at {guild} ({guildId}) roles updated", user.DisplayName, userId, guild.Name, guildId);
            await this.SetAsync(new() { GuildId = guildId, UserId = userId, RoleIds = user.Roles.Select(role => role.Id).Where(roleId => roleId != guildId) });
        }

        public async Task<RoleAction> GetRoleActionCoreAsync(GuildConfiguration guildConf, RoleConfiguration roleConf, CancellationToken cancellationToken = default)
        {
            var guildId = guildConf.GuildId;
            var roleId = roleConf.RoleId;

            Debug.Assert(roleConf.GuildId == guildConf.GuildId);

            // Get guild and role information
            var guild = this.Discord.GetGuild(guildId);
            var role = await guild.GetRoleAsync(roleId, new() { CancelToken = cancellationToken });

            // Dangerous role check
            var isAdmin = role?.IsDangerous() is true; // NOTE: Do not remove null check

            // Admin roles special cases
            if (isAdmin)
            {
                // Special Case 1: Always ignore admin roles
                if (guildConf.IgnoreDangerous is IgnoreMode.Always) return RoleAction.Ignore;

                // Special Case 2: Ignore admin roles if default
                if (roleConf.Action is RoleAction.Default && guildConf.IgnoreDangerous is IgnoreMode.DefaultOnly) return RoleAction.Ignore;
            }

            // Determine action to take
            var action = roleConf.Action;
            if (action is RoleAction.Default) action = guildConf.DefaultAction;
            if (action is RoleAction.Default) action = RoleAction.Persist;
            return action;
        }
    }
}
