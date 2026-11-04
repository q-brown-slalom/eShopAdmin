namespace eShopAdmin.Auth;

public static class SessionLifetime
{
    // The cookie ends a minute before the access token so the API never sees an expired token.
    private const int SafetyMarginSeconds = 60;

    public static DateTimeOffset ExpiresAt(DateTimeOffset now, int accessTokenExpiresInSeconds)
        => now.AddSeconds(Math.Max(0, accessTokenExpiresInSeconds - SafetyMarginSeconds));
}
