using Discord;
using Discord.WebSocket;
using DryIocAttributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RolesWarden.Db;
using RolesWarden.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RolesWarden.Services
{
    [ExportMany]
    [SingletonReuse]
    public sealed class RoleConfigurationService : IHostedService
    {
        private IDbContextFactory<WardenDbContext> DbPool { get; }
        private DiscordSocketClient Discord { get; }
        private ILogger Logger { get; }

        public RoleConfigurationService(IDbContextFactory<WardenDbContext> dbPool, DiscordSocketClient discord, ILogger<RoleConfigurationService> logger)
        {
            CommonLog.LogConstructing(logger, this);
            this.DbPool = dbPool;
            this.Discord = discord;
            this.Logger = logger;
            CommonLog.LogConstructed(logger, this);
        }

        private async Task Discord_RoleDeleted(SocketRole role)
        {
            this.Logger.LogInformation("Removing role configuration for {guild} ({guildId}) => {role} ({roleId}): Role was deleted", role.Guild.Name, role.Guild.Id, role.Name, role.Id);
            await this.RemoveAsync(role);
        }

        private async Task Discord_ResetGuild(SocketGuild guild)
        {
            this.Logger.LogInformation("Removing role configurations for {guild} ({guildId}): Bot is no longer at the server", guild.Name, guild.Id);
            await this.ClearGuildAsync(guild);
        }

        public async Task<RoleConfiguration> GetAsync(IRole role, CancellationToken cancellationToken = default)
        {
            await using var dbContext = await this.DbPool.CreateDbContextAsync(cancellationToken);
            return await GetCoreAsync(dbContext, role, cancellationToken);
        }

        public async Task<IEnumerable<RoleConfiguration>> GetAsync(IEnumerable<ulong> roleIds, ulong defaultGuildId = 0, CancellationToken cancellationToken = default)
        {
            await using var dbContext = await this.DbPool.CreateDbContextAsync(cancellationToken);
            return await GetCoreAsync(dbContext, roleIds, defaultGuildId, cancellationToken);
        }

        public Task<IEnumerable<RoleConfiguration>> GetAsync(IEnumerable<IRole> roles, ulong defaultGuildId = 0, CancellationToken cancellationToken = default)
        {
            return this.GetAsync(roles.Select(role => role.Id), defaultGuildId, cancellationToken);
        }

        public async Task SetAsync(RoleConfiguration configuration, CancellationToken cancellationToken = default)
        {
            await using var dbContext = await this.DbPool.CreateDbContextAsync(cancellationToken);
            await SetCoreAsync(dbContext, configuration, cancellationToken);
        }

        public Task SetAsync(IRole role, RoleAction action, CancellationToken cancellationToken = default)
        {
            return this.SetAsync(new() { RoleId = role.Id, GuildId = role.Guild.Id, Action = action }, cancellationToken);
        }

        public async Task<bool> RemoveAsync(RoleConfiguration configuration, CancellationToken cancellationToken = default)
        {
            await using var dbContext = await this.DbPool.CreateDbContextAsync(cancellationToken);
            return await RemoveCoreAsync(dbContext, configuration, cancellationToken);
        }

        public Task RemoveAsync(IRole role, CancellationToken cancellationToken = default)
        {
            return this.RemoveAsync(role.Id, cancellationToken);
        }

        public async Task<bool> RemoveAsync(ulong roleId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = await this.DbPool.CreateDbContextAsync(cancellationToken);
            return await RemoveCoreAsync(dbContext, roleId, cancellationToken);
        }

        public async Task<IEnumerable<RoleConfiguration>> GetGuildRolesAsync(ulong guildId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = await this.DbPool.CreateDbContextAsync(cancellationToken);
            return await GetGuildRolesCoreAsync(dbContext, guildId, cancellationToken);
        }

        public Task<IEnumerable<RoleConfiguration>> GetGuildRolesAsync(IGuild guild, CancellationToken cancellationToken = default)
        {
            return this.GetGuildRolesAsync(guild.Id, cancellationToken);
        }

        public async Task<int> ClearGuildAsync(ulong guildId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = await this.DbPool.CreateDbContextAsync(cancellationToken);
            return await ClearGuildCoreAsync(dbContext, guildId, cancellationToken);
        }

        public Task<int> ClearGuildAsync(IGuild guild, CancellationToken cancellationToken = default)
        {
            return this.ClearGuildAsync(guild.Id, cancellationToken);
        }

        public static async Task<RoleConfiguration> GetCoreAsync(WardenDbContext dbContext, IRole role, CancellationToken cancellationToken = default)
        {
            return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(role, async (role, ct) =>
            {
                var guild = role.Guild;
                var result = await dbContext.RoleConfigurations.FindAsync([role.Id], ct) ?? new() { RoleId = role.Id, GuildId = guild.Id };
                return result;
            }, cancellationToken);
        }

        public static async Task<IEnumerable<RoleConfiguration>> GetCoreAsync(WardenDbContext dbContext, IEnumerable<ulong> roleIds, ulong defaultGuildId = 0, CancellationToken cancellationToken = default)
        {
            return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(roleIds, async (roleIds, ct) =>
            {
                var result = await dbContext.RoleConfigurations.Where(roles => roleIds.Contains(roles.RoleId)).ToDictionaryAsync(roles => roles.RoleId, roles => roles, cancellationToken);
                foreach (var roleId in roleIds)
                {
                    ref var roles = ref CollectionsMarshal.GetValueRefOrAddDefault(result, roleId, out var exists);
                    if (!exists) roles = new() { RoleId = roleId, GuildId = defaultGuildId };
                }
                return result.Values;
            }, cancellationToken);
        }

        public static async Task SetCoreAsync(WardenDbContext dbContext, RoleConfiguration configuration, CancellationToken cancellationToken = default)
        {
            if (configuration.Action is RoleAction.Default)
            {
                await RemoveCoreAsync(dbContext, configuration, cancellationToken);
                return;
            }

            await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(configuration, async (configuration, ct) =>
            {
                if (await dbContext.RoleConfigurations.ContainsAsync(configuration, ct))
                {
                    var entry = dbContext.RoleConfigurations.Attach(configuration);
                    entry.State = EntityState.Modified;
                }
                else
                {
                    await dbContext.RoleConfigurations.AddAsync(configuration, ct);
                }
                await dbContext.SaveChangesAsync(ct);
            }, cancellationToken);
        }

        public static async Task<bool> RemoveCoreAsync(WardenDbContext dbContext, RoleConfiguration configuration, CancellationToken cancellationToken = default)
        {
            return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(configuration, async (configuration, ct) =>
            {
                dbContext.RoleConfigurations.Remove(configuration);
                return await dbContext.SaveChangesAsync(ct) > 0;
            }, cancellationToken);
        }

        public static async Task<bool> RemoveCoreAsync(WardenDbContext dbContext, ulong roleId, CancellationToken cancellationToken = default)
        {
            return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(roleId, async (roleId, ct) =>
            {
                return await dbContext.RoleConfigurations.Where(configuration => configuration.RoleId == roleId).ExecuteDeleteAsync(ct) > 0;
            }, cancellationToken);
        }

        public static async Task<IEnumerable<RoleConfiguration>> GetGuildRolesCoreAsync(WardenDbContext dbContext, ulong guildId, CancellationToken cancellationToken = default)
        {
            return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(guildId, async (guildId, ct) =>
            {
                return await dbContext.RoleConfigurations.Where(configuration => configuration.GuildId == guildId).ToListAsync(ct);
            }, cancellationToken);
        }

        public static async Task<int> ClearGuildCoreAsync(WardenDbContext dbContext, ulong guildId, CancellationToken cancellationToken = default)
        {
            return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(guildId, async (guildId, ct) =>
            {
                return await dbContext.RoleConfigurations.Where(configuration => configuration.GuildId == guildId).ExecuteDeleteAsync(ct);
            }, cancellationToken);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            CommonLog.LogStarting(this.Logger, this);
            this.Discord.LeftGuild += Discord_ResetGuild;
            this.Discord.JoinedGuild += Discord_ResetGuild;
            this.Discord.RoleDeleted += Discord_RoleDeleted;
            CommonLog.LogStarted(this.Logger, this);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            CommonLog.LogStopping(this.Logger, this);
            this.Discord.LeftGuild -= Discord_ResetGuild;
            this.Discord.JoinedGuild -= Discord_ResetGuild;
            this.Discord.RoleDeleted -= Discord_RoleDeleted;
            CommonLog.LogStopped(this.Logger, this);
            return Task.CompletedTask;
        }
    }
}
