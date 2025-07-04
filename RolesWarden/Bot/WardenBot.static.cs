using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;

namespace RolesWarden.Bot
{
    public sealed partial class WardenBot
    {
        private static DiscordSocketClient CreateClient()
        {
            var options = new DiscordSocketConfig()
            {
                AlwaysDownloadDefaultStickers = false,
                AlwaysResolveStickers = false,
                AlwaysDownloadUsers = false,
                GatewayIntents = GatewayIntents.GuildMembers | GatewayIntents.Guilds,
            };
            return new(options);
        }

        private static InteractionService CreateInteractions(DiscordSocketClient client)
        {
            var options = new InteractionServiceConfig()
            {
                InteractionCustomIdDelimiters = ['.'],
                EnableAutocompleteHandlers = true,
            };
            return new(client, options);
        }

        private static CommandService CreateCommands()
        {
            var options = new CommandServiceConfig();
            return new(options);
        }
    }
}
