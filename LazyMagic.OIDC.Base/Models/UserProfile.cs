namespace LazyMagic.OIDC.Base
{
    /// <summary>
    /// Simple model class for user profile
    /// </summary>
    public class UserProfile
    {
        public string Id { get; set; } = "";
        public string Email { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Bio { get; set; }
        public DateTime LastLogin { get; set; }
    }
}