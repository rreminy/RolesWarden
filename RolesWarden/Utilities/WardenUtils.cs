using Discord;

namespace RolesWarden.Utilities
{
    public static class WardenUtils
    {
        /// <summary>Creates an empty <see cref="EmbedBuilder"/> with some footer information added.</summary>
        /// <returns>Empty <see cref="EmbedBuilder"/> with some footer information added.</returns>
        public static EmbedBuilder CreateEmbed()
        {
            return new EmbedBuilder()
                .WithFooter($"{typeof(WardenUtils).Assembly.GetName().Name} v{typeof(WardenUtils).Assembly.GetName().Version}");
        }
    }
}
