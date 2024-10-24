namespace LazyMagic.Client.CognitoValidation
{
    public interface ITokenValidator
    {
        Task<bool> ValidateTokenHttpAsync(string? token);
    }
}