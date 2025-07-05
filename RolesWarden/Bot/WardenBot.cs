using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using DryIocAttributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace RolesWarden.Bot
{
    [ExportMany]
    [SingletonReuse]
    public sealed partial class WardenBot : IHostedService, IDisposable, IAsyncDisposable
    {
        public const string ChatCommandPrefix = "$$";
        private bool _commandsInstalled;

        public DiscordSocketClient Client { get; }
        public CommandService Commands { get; }
        public InteractionService Interactions { get; }

        private IConfiguration Configuration { get; }
        private ILogger Logger { get; }

        public WardenBot(IConfiguration configuration, ILogger<WardenBot> logger)
        {
            CommonLog.LogConstructing(logger, this);

            this.Configuration = configuration.GetSection("Discord");
            this.Logger = logger;

            var client = CreateClient();
            var commands = CreateCommands();
            var interactions = CreateInteractions(client);

            client.Log += this.DiscordLogHandler;
            commands.Log += this.DiscordLogHandler;
            interactions.Log += this.DiscordLogHandler;

            client.Ready += this.DiscordReady;
            client.MessageReceived += this.DiscordMessageHandler;
            client.InteractionCreated += this.DiscordInteractionHandler;

            this.Client = client;
            this.Commands = commands;
            this.Interactions = interactions;

            CommonLog.LogConstructed(logger, this);
        }

        private async Task DiscordReady()
        {
            this.Logger.LogInformation("Discord Bot ready: {name}#{discriminator} ({id})", this.Client.CurrentUser.Username, this.Client.CurrentUser.Discriminator, this.Client.CurrentUser.Id);
            var id = this.Client.CurrentUser.Id;
            var permissions = GuildPermission.ManageRoles | GuildPermission.SendMessages | GuildPermission.EmbedLinks;
            this.Logger.LogInformation("Bot Invite: https://discord.com/oauth2/authorize?client_id={id}&scope=bot+applications.commands&permissions={permissions}", id, (ulong)permissions);
        }

        private async Task InstallCommandsAndInteractions()
        {
            if (Interlocked.CompareExchange(ref this._commandsInstalled, true, false)) return;
            await this.Commands.AddModulesAsync(Assembly.GetExecutingAssembly(), Program.Application.Services);
            await this.Interactions.AddModulesAsync(Assembly.GetExecutingAssembly(), Program.Application.Services);
            this.Logger.LogInformation("Discord commands and interactions installed");
        }

        private Task DiscordLogHandler(LogMessage log)
        {
            var level = log.Severity switch
            {
                LogSeverity.Verbose => LogLevel.Trace,
                LogSeverity.Debug => LogLevel.Debug,
                LogSeverity.Info => LogLevel.Information,
                LogSeverity.Warning => LogLevel.Warning,
                LogSeverity.Error => LogLevel.Error,
                LogSeverity.Critical => LogLevel.Critical,
                _ => (LogLevel)log.Severity, // ASSERT: Should never happen
            };
            this.Logger.Log(level, log.Exception, "[{source}] {message}", log.Source, log.Message);
            return Task.CompletedTask;
        }

        private Task DiscordMessageHandler(SocketMessage messageParam)
        {
            if (messageParam is not SocketUserMessage message) return Task.CompletedTask;
            var argPos = 0;
            if (!message.HasStringPrefix(ChatCommandPrefix, ref argPos) && !message.HasMentionPrefix(this.Client.CurrentUser, ref argPos)) return Task.CompletedTask;
            _ = this.DiscordMessageHandlerCore(message, argPos);
            return Task.CompletedTask;
        }

        private async Task DiscordMessageHandlerCore(SocketUserMessage message, int argPos)
        {
            var input = message.Content[argPos..];
            try
            {
                var context = new SocketCommandContext(this.Client, message);
                var result = await this.Commands.ExecuteAsync(context, input, Program.Application.Services);
                if (result.Error is not null && result.Error is not CommandError.UnknownCommand)
                {
                    this.Logger.LogError("Error executing command: {type} - {reason}", result.Error, result.ErrorReason);
                    await context.Channel.SendMessageAsync($":no_entry: {result.Error} - {result.ErrorReason}");
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Unhandled exception handling command: {input}", input);
            }
        }

        private Task DiscordInteractionHandler(SocketInteraction interaction)
        {
            _ = this.DiscordInteractionHandlerCore(interaction);
            return Task.CompletedTask;
        }

        private async Task DiscordInteractionHandlerCore(SocketInteraction interaction)
        {
            try
            {
                var context = new SocketInteractionContext(this.Client, interaction);
                var result = await this.Interactions.ExecuteCommandAsync(context, Program.Application.Services);
                this.Logger.LogWarning("Error executing interaction: {type} - {reason}", result.Error, result.ErrorReason);
                //if (result.Error is not null) await InteractionErrorUtils.LogInteractionErrorAsync(interaction, context, result); //result.Error is not InteractionCommandError.UnknownCommand
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Unhandled exception handling interaction");
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var token = this.Configuration.GetValue<string>("Token");
            if (string.IsNullOrWhiteSpace(token)) return;

            CommonLog.LogStarting(this.Logger, this);
            await this.InstallCommandsAndInteractions();
            await this.Client.LoginAsync(TokenType.Bot, token);
            await this.Client.StartAsync();
            CommonLog.LogStarted(this.Logger, this);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            CommonLog.LogStopping(this.Logger, this);
            await this.Client.StopAsync();
            await this.Client.LogoutAsync();
            CommonLog.LogStopped(this.Logger, this);
        }

        public async ValueTask DisposeAsync()
        {
            CommonLog.LogDisposing(this.Logger, this);
            this.Interactions.Dispose();
            await this.Client.DisposeAsync();
            CommonLog.LogDisposed(this.Logger, this);
        }

        public void Dispose()
        {
            CommonLog.LogDisposing(this.Logger, this);
            this.Interactions.Dispose();
            this.Client.Dispose();
            CommonLog.LogDisposed(this.Logger, this);
        }
    }
}
