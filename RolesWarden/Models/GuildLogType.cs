using System;

namespace RolesWarden.Models
{
    [Flags]
    public enum GuildLogType
    {
        None = 0,
        Restored = 1,
        Saved = 2,
    }
}
