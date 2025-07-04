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
    public sealed class SavedRolesService : IHostedService
    {
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
            await this.SetAsync(new() { GuildId = guildId, UserId = userId, RoleIds = roles.ToHashSet() }, cancellationToken);
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

        private async Task Discord_GuildMemberUpdated(Cacheable<SocketGuildUser, ulong> old, SocketGuildUser user)
        {
            if (old.HasValue)
            {
                var oldRoles = old.Value.Roles.ToHashSet();
                var newRoles = user.Roles.ToHashSet();
                if (oldRoles.SetEquals(newRoles)) return;
            }
            await this.SaveRoles(user);
        }

        private async Task RestoreRoles(SocketGuildUser user)
        {
            var userId = user.Id;
            var guild = user.Guild;
            var guildId = guild.Id;
            this.Logger.LogInformation("User {user} ({userId}) joined {guild} ({guildId}), restoring roles", user.DisplayName, userId, guild.Name, guildId);

            var roles = await this.GetAsync(user);
            var guildConfig = await this.GuildConfigs.GetAsync(guildId);
            var roleConfigs = await this.RoleConfigs.GetAsync(roles.RoleIds ?? FrozenSet<ulong>.Empty, guildId);

            Debug.Assert(roleConfigs.All(config => config.GuildId == guildId));

            var success = new List<ulong>();
            var failed = new List<ulong>();
            var ignored = new List<ulong>();
            foreach (var roleConfig in roleConfigs)
            {
                var action = await GetRoleActionCoreAsync(guildConfig, roleConfig);
                if (action is RoleAction.Persist && await user.Guild.GetRoleAsync(roleConfig.RoleId) is not null)
                {
                    try
                    {
                        var roleId = roleConfig.RoleId;
                        await user.AddRoleAsync(roleId);
                        success.Add(roleId);
                    }
                    catch
                    {
                        failed.Add(roleConfig.RoleId);
                    }
                }
                else ignored.Add(roleConfig.RoleId);
            }
            this.Logger.LogInformation("User {user} ({userId}) at {guild} ({guildId}) roles restored (Success: {success} | Ignored: {ignored} | Failed: {failed})", user.DisplayName, userId, guild.Name, guildId, string.Join(", ", success), string.Join(", ", ignored), string.Join(", ", failed));
        }

        private async Task SaveRoles(SocketGuildUser user)
        {
            var userId = user.Id;
            var guild = user.Guild;
            var guildId = guild.Id;
            this.Logger.LogInformation("User {user} ({userId}) at {guild} ({guildId}) roles updated", user.DisplayName, userId, guild.Name, guildId);
            await this.SetAsync(new() { GuildId = guildId, UserId = userId, RoleIds = user.Roles.Select(role => role.Id).ToHashSet() });
        }

        public async Task<RoleAction> GetRoleActionCoreAsync(GuildConfiguration guildConf, RoleConfiguration roleConf, CancellationToken cancellationToken = default)
        {
            var guildId = guildConf.GuildId;
            var roleId = roleConf.RoleId;

            Debug.Assert(roleConf.GuildId == guildConf.GuildId);

            // Get guild and role information
            var guild = this.Discord.GetGuild(guildId);
            var role = await guild.GetRoleAsync(roleId, new() { CancelToken = cancellationToken });

            // Is admin check
            var isAdmin = role?.Permissions.Administrator is true; // NOTE: Do not remove null check

            // Admin roles special cases
            if (isAdmin)
            {
                // Special Case 1: Always ignore admin roles
                if (guildConf.IgnoreAdmin is IgnoreAdminMode.Always) return RoleAction.Ignore;

                // Special Case 2: Ignore admin roles if default
                if (roleConf.Action is RoleAction.Default && guildConf.IgnoreAdmin is IgnoreAdminMode.DefaultOnly) return RoleAction.Ignore;
            }

            // Determine action to take
            var action = roleConf.Action;
            if (action is RoleAction.Default) action = guildConf.DefaultAction;
            if (action is RoleAction.Default) action = RoleAction.Persist;
            return action;
        }
    }
}
