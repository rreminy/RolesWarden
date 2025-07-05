using Discord.Commands;
using System.Threading.Tasks;

namespace RolesWarden.Bot.Commands
{
    [RequireOwner]
    [RequireContext(ContextType.DM)]
    public sealed class SystemCommands : ModuleBase
    {
        private WardenBot Bot { get; }
        
        public SystemCommands(WardenBot bot)
        {
            this.Bot = bot;
        }

        [Command("register-warden-interactions")]
        public async Task RegisterWardenInteractionsAsync()
        {
            var message = await this.Context.Channel.SendMessageAsync("Registering interaction commands...");
            await this.Bot.Interactions.RegisterCommandsGloballyAsync();
            await message.ModifyAsync(message => message.Content = "Interaction commands registered!");
        }
    }
}
