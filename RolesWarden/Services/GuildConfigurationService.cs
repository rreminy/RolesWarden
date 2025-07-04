using Discord;
using Discord.WebSocket;
using DryIocAttributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RolesWarden.Db;
using RolesWarden.Models;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RolesWarden.Services
{
    [ExportMany]
    [SingletonReuse]
    public sealed class GuildConfigurationService : IHostedService
    {
        private IDbContextFactory<WardenDbContext> DbPool { get; }
        private DiscordSocketClient Discord { get; }
        private ILogger Logger { get; }

        public GuildConfigurationService(IDbContextFactory<WardenDbContext> dbPool, DiscordSocketClient discord, ILogger<GuildConfigurationService> logger)
        {
            CommonLog.LogConstructing(logger, this);
            DbPool = dbPool;
            Discord = discord;
            Logger = logger;
            CommonLog.LogConstructed(logger, this);
        }

        private Task Discord_ResetGuild(SocketGuild guild)
        {
            this.Logger.LogInformation("Removing guild configuration for {guild} ({guildId}): Bot is no longer at the server", guild.Name, guild.Id);
            return this.RemoveAsync(guild);
        }

        public async Task<GuildConfiguration> GetAsync(ulong guildId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = await this.DbPool.CreateDbContextAsync(cancellationToken);
            return await GetCoreAsync(dbContext, guildId, cancellationToken);
        }

        public Task<GuildConfiguration> GetAsync(IGuild guild, CancellationToken cancellationToken = default)
        {
            return this.GetAsync(guild.Id, cancellationToken);
        }

        public async Task SetAsync(GuildConfiguration configuration, CancellationToken cancellationToken = default)
        {
            await using var dbContext = await this.DbPool.CreateDbContextAsync(cancellationToken);
            await SetCoreAsync(dbContext, configuration, cancellationToken);
        }

        public async Task<bool> RemoveAsync(GuildConfiguration configuration, CancellationToken cancellationToken = default)
        {
            await using var dbContext = await this.DbPool.CreateDbContextAsync(cancellationToken);
            return await RemoveCoreAsync(dbContext, configuration, cancellationToken);
        }

        public async Task<bool> RemoveAsync(ulong guildId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = await this.DbPool.CreateDbContextAsync(cancellationToken);
            return await RemoveCoreAsync(dbContext, guildId, cancellationToken);
        }

        public Task<bool> RemoveAsync(IGuild guild, CancellationToken cancellationToken = default)
        {
            return this.RemoveAsync(guild.Id, cancellationToken);
        }

        public static async Task<GuildConfiguration> GetCoreAsync(WardenDbContext dbContext, ulong guildId, CancellationToken cancellationToken = default)
        {
            return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(guildId, async (guildId, ct) =>
            {
                return (await dbContext.GuildConfigurations.FindAsync([guildId], cancellationToken)) ?? new() { GuildId = guildId };
            }, cancellationToken);
        }

        public static async Task SetCoreAsync(WardenDbContext dbContext, GuildConfiguration configuration, CancellationToken cancellationToken = default)
        {
            await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(configuration, async (configuration, ct) =>
            {
                if (await dbContext.GuildConfigurations.ContainsAsync(configuration, ct))
                {
                    var entry = dbContext.GuildConfigurations.Attach(configuration);
                    entry.State = EntityState.Modified;
                }
                else
                {
                    await dbContext.GuildConfigurations.AddAsync(configuration, ct);
                }
                await dbContext.SaveChangesAsync(ct);
            }, cancellationToken);
        }

        public static async Task<bool> RemoveCoreAsync(WardenDbContext dbContext, GuildConfiguration configuration, CancellationToken cancellationToken = default)
        {
            return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(configuration, async (configuration, ct) =>
            {
                dbContext.GuildConfigurations.Remove(configuration);
                return await dbContext.SaveChangesAsync(ct) > 0;
            }, cancellationToken);
        }

        public static async Task<bool> RemoveCoreAsync(WardenDbContext dbContext, ulong guildId, CancellationToken cancellationToken = default)
        {
            return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(guildId, async (configuration, ct) =>
            {
                return await dbContext.GuildConfigurations.Where(configuration => configuration.GuildId == guildId).ExecuteDeleteAsync(ct) > 0;
            }, cancellationToken);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            CommonLog.LogStarting(this.Logger, this);
            this.Discord.LeftGuild += Discord_ResetGuild;
            this.Discord.JoinedGuild += Discord_ResetGuild;
            CommonLog.LogStarted(this.Logger, this);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            CommonLog.LogStopping(this.Logger, this);
            this.Discord.LeftGuild -= Discord_ResetGuild;
            this.Discord.JoinedGuild -= Discord_ResetGuild;
            CommonLog.LogStopped(this.Logger, this);
            return Task.CompletedTask;
        }
    }
}
