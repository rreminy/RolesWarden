using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace RolesWarden.Models
{
    [PrimaryKey(nameof(GuildId), nameof(UserId))]
    [Table("saved_roles")]
    public sealed class SavedRoles
    {
        [Column("guild_id")]
        public required ulong GuildId { get; init; }

        [Column("user_id")]
        public required ulong UserId { get; init; }

        [Column("roles_ids")]
        public ISet<ulong>? RoleIds { get; set; }

        [Column("timestamp")]
        public long Timestamp { get; set; }
    }
}
