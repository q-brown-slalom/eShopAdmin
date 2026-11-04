namespace eShopAdmin.BusinessRules;

/// <summary>BusinessRules.API rejected the access token (expired or revoked); the user must sign in again.</summary>
public sealed class ReauthenticationRequiredException : Exception
{
    public ReauthenticationRequiredException()
        : base("BusinessRules.API rejected the access token.")
    {
    }
}
