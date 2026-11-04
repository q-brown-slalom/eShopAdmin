using eShopAdmin.Auth;

namespace eShopAdmin.UnitTests.Auth;

[TestClass]
public class SessionLifetimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void SessionEndsBeforeAccessToken()
        => Assert.AreEqual(Now.AddSeconds(3540), SessionLifetime.ExpiresAt(Now, 3600));

    [TestMethod]
    public void SessionLifetimeNeverNegative()
        => Assert.AreEqual(Now, SessionLifetime.ExpiresAt(Now, 30));

    [TestMethod]
    public void SessionLifetimeIsZeroForNegativeExpiry()
        => Assert.AreEqual(Now, SessionLifetime.ExpiresAt(Now, -5));
}
