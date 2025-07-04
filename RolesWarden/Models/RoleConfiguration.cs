using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace RolesWarden.Models
{
    [PrimaryKey(nameof(RoleId))]
    [Index(nameof(GuildId))]
    [Table("role_config")]
    public sealed class RoleConfiguration : IEquatable<RoleConfiguration>
    {
        [Column("role_id")]
        public required ulong RoleId { get; init; }

        [Column("guild_id")]
        public required ulong GuildId { get; init; }

        [Column("action")]
        public RoleAction Action { get; set; }

        public static bool Equals(RoleConfiguration? left, RoleConfiguration? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            return left.RoleId == right.RoleId;
        }

        public bool Equals(RoleConfiguration? other) => Equals(this, other);

        public override bool Equals(object? obj) => obj is RoleConfiguration other && Equals(this, other);

        public override int GetHashCode() => this.RoleId.GetHashCode();
    }
}
