using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace RolesWarden.Models
{
    [PrimaryKey(nameof(GuildId))]
    [Table("guild_config")]
    public sealed class GuildConfiguration : IEquatable<GuildConfiguration>
    {
        [Column("guild_id")]
        public required ulong GuildId { get; init; }

        [Column("default_action")]
        public RoleAction DefaultAction { get; set; }

        [Column("ignore_admin")]
        public IgnoreAdminMode IgnoreAdmin { get; set; }

        [Column("log_channel_id")]
        public ulong LogChannelId { get; set; }

        [Column("log_types")]
        public GuildLogType LogTypes { get; set; }

        public static bool Equals(GuildConfiguration? left, GuildConfiguration? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            return left.GuildId == right.GuildId;
        }

        public bool Equals(GuildConfiguration? other) => Equals(this, other);

        public override bool Equals(object? obj) => obj is GuildConfiguration other && Equals(this, other);

        public override int GetHashCode() => this.GuildId.GetHashCode();
    }
}
