namespace LazyMagic.Client.CognitoValidation;

public class TokenValidator : ITokenValidator
{
    private readonly HttpClient _httpClient;
    private readonly string _jwksUrl;
    private JsonWebKeySet? _jwks;
    private readonly TokenValidationParameters _tokenParams;
    private DateTime _lastJWKSFetch = DateTime.MinValue;

    public TokenValidator(string cognitoRegion, string userPoolId)
    {
        _httpClient = new HttpClient();
        _jwksUrl = $"https://cognito-idp.{cognitoRegion}.amazonaws.com/{userPoolId}/.well-known/jwks.json";
        _tokenParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://cognito-idp.{cognitoRegion}.amazonaws.com/{cognitoRegion}",
            ValidateAudience = false,
            ValidateLifetime = true
        };
    }
    public async Task<bool> ValidateTokenHttpAsync(string? token)
    {
        if (token is null) return false;

        try
        {
            // Fetch new JWKS keys if an hour has passed since the last fetch
            // I believe Cognito overlaps keys. Since the default expiration time 
            // for a Cognito JWT is 1 hour, its conservative to use that as the time between
            // JWKS keys retreival. This keeps the calls to the Cognito jwksUrl 
            // minimal. 
            if (_jwks is null || (DateTime.UtcNow - _lastJWKSFetch) > TimeSpan.FromHours(1))
            {
                _jwks = await FetchJwksAsync();
                _lastJWKSFetch = DateTime.UtcNow;
            }

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            _tokenParams.IssuerSigningKeyResolver = (s, securityToken, identifier, parameters) =>
            {
                var now = DateTime.UtcNow;
                var validKeys = _jwks.Keys.Where(key => key.KeyId == identifier).ToList();
                if (validKeys.Count > 1)
                    throw new InvalidOperationException("Found more than one JWKS entry for Id");
                return (IEnumerable<SecurityKey>)validKeys;
            };

            var user = handler.ValidateToken(token, _tokenParams, out var validatedToken);
            return validatedToken != null;
        }
        catch
        {
            return false;
        }
    }

    private async Task<JsonWebKeySet> FetchJwksAsync()
    {
        var response = await _httpClient.GetStringAsync(_jwksUrl);
        return new JsonWebKeySet(response);
    }
}