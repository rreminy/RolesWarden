using Discord;
using RolesWarden.Models;
using RolesWarden.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RolesWarden.Services
{
    public sealed partial class SavedRolesService
    {
        public static async Task<bool> CheckPermissionsAsync(ITextChannel channel)
        {
            var guild = channel.Guild;
            if (guild is null) return false;

            var botUser = await guild.GetCurrentUserAsync();
            if (botUser is null) return false; // Should not happen? I don't trust this without nullable annotations so test anyway

            var guildPermissions = botUser.GuildPermissions;
            if (!guildPermissions.ManageRoles) return false;

            var channelPermissions = botUser.GetPermissions(channel);
            if (!channelPermissions.SendMessages || !channelPermissions.EmbedLinks) return false;

            return true;
        }
        
        private static EmbedBuilder CreateRestoredEmbed(ulong userId, List<ulong> restored, List<KeyValuePair<ulong, Exception>> failed)
        {
            var embed = WardenUtils.CreateEmbed()
                .WithTitle("User roles restored");

            embed.AddField("User", $"<@{userId}>", true);

            var restoredText = restored.Count > 0 ? string.Join(", ", restored.Select(roleId => $"<@&{roleId}>")) : "No roles restored";
            embed.AddField("Roles", restoredText, false);

            if (failed.Count > 0)
            {
                embed.AddField("Failed", string.Join("\n", failed.Select(failed => $"<@&{failed.Key}> - {failed.Value.Message}")), false);
            }
            return embed;
        }

        private static EmbedBuilder CreateSavedEmbed(SavedRoles roles)
        {
            // This makes it look like the roles are saved on leave
            // however roles are actually saved every user update.

            var embed = WardenUtils.CreateEmbed()
                .WithTitle("User roles saved");

            embed.AddField("User", $"<@{roles.UserId}>", true);

            var roleIds = roles.RoleIds;
            if (roleIds is not null) embed.AddField("Roles", string.Join(", ", roleIds.Select(roleId => $"<@&{roleId}>")), false);
            else embed.AddField("Roles", "No roles saved", false);

            return embed;
        }
    }
}
