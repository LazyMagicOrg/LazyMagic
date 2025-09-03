namespace LazyMagic.OIDC.MAUI;

/// <summary>
/// Interface for handling custom URI schemes
/// </summary>
public interface IUriHandler
{
    /// <summary>
    /// Handles incoming URI from custom scheme
    /// </summary>
    Task<bool> HandleUriAsync(string uri);
}