namespace RolesWarden.Models
{
    public enum IgnoreMode
    {
        /// <summary>Ignore admin roles by default.</summary>
        DefaultOnly,

        /// <summary>Always ignore admin roles, even if configure to <see cref="RoleAction.Persist"/>.</summary>
        Always,

        /// <summary>Never ignore admin roles. VERY INSECURE!</summary>
        Never,
    }
}
