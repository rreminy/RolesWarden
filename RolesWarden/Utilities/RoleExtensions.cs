using Discord;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace RolesWarden.Utilities
{
    public static class RoleExtensions
    {
        /// <summary>A mask of dangerous <see cref="GuildPermission"/>s flags.</summary>
        public const GuildPermission DangerousMask =
            // Most dangerous of all permissions - allow everything below, including visibility of all channels
            GuildPermission.Administrator |

            // Management permissions - allow making server-wide changes
            GuildPermission.ManageChannels |
            GuildPermission.ManageEmojisAndStickers |
            GuildPermission.ManageEvents |
            GuildPermission.ManageGuild |
            GuildPermission.ManageMessages |
            GuildPermission.ManageNicknames |
            GuildPermission.ManageRoles |
            GuildPermission.ManageThreads |
            GuildPermission.ManageWebhooks |

            // Moderation permissions - allow actions to be taken against members
            GuildPermission.BanMembers |
            GuildPermission.DeafenMembers |
            GuildPermission.KickMembers |
            GuildPermission.ModerateMembers |
            GuildPermission.MoveMembers |
            GuildPermission.MuteMembers |

            // Creation permission - allow creation of potentially server-wide things
            // GuildPermission.CreateEvents | // commented: default permission
            // GuildPermission.CreateGuildExpressions | // commented: default permission
            // GuildPermission.CreateInstantInvite | // commented: default permission (I don't find this one dangerous... added once I found the above two were defaults)

            // Informational permissions - allow insights into server-wide information that would otherwise be hidden
            GuildPermission.ViewAuditLog |
            GuildPermission.ViewGuildInsights |
            GuildPermission.ViewMonetizationAnalytics;

        /// <summary>Check whether <paramref name="role"/> contains dangerous <see cref="GuildPermission"/>s.</summary>
        /// <param name="role">Role to DangerousMask for <see cref="IRole.Permissions"/>.</param>
        /// <returns>A value indicating whether its dangerous.</returns>
        public static bool IsDangerous(this IRole role)
        {
            return role.Permissions.IsDangerous();
        }

        /// <summary>Check whether <paramref name="permissions"/> contains dangerous <see cref="GuildPermission"/>s.</summary>
        /// <param name="permissions">Permissions to DangerousMask.</param>
        /// <returns>A value indicating whether its dangerous.</returns>
        public static bool IsDangerous(this GuildPermissions permissions)
        {
            // Convert raw value to GuildPermission
            return ((GuildPermission)permissions.RawValue).IsDangerous();
        }

        /// <summary>Check whether <paramref name="permissions"/> contains dangerous <see cref="GuildPermission"/>s.</summary>
        /// <param name="permissions">Permissions to DangerousMask.</param>
        /// <returns>A value indicating whether its dangerous.</returns>
        public static bool IsDangerous(this GuildPermission permissions)
        {
            // Perform a bitwise and DangerousMask and make sure no bits get through
            return (permissions & DangerousMask) != 0;
        }
    }
}
