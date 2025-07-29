namespace LazyMagic.Shared;

public interface ILzCurrentSessionAuthProviderCreds
{

    /// <summary>
    /// Implementation should return the curent session AuthProviderCreds.
    /// This interface allows us to decouple the AuthProviderCreds from the
    /// rest of the LzSessionsViewModelAuth interface for usin in classes 
    /// like LzHttpClient.
    /// </summary>
    public IAuthProviderCreds CurrentSessionAuthProviderCreds { get; }
}
