using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;

namespace eShopAdmin.UnitTests.Hosting;

[TestClass]
public class ErrorAndReauthenticationTests
{
    private const string ErrorCopy = "Something went wrong talking to the rules service. Try again in a minute.";

    private static HttpResponseMessage Status(HttpStatusCode code) => new(code);

    private static async Task<HttpResponseMessage> GetAsAsync(AdminAppFactory factory, string url, string? role = "RulesEditor", string? cookie = null)
    {
        using var client = factory.CreateAppClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (role is not null)
        {
            request.Headers.Add(AdminAppFactory.RoleHeader, role);
        }

        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        return await client.SendAsync(request);
    }

    [TestMethod]
    public async Task ExpiredTokenRedirectsToIdentityAndComesBackToTheSameUrl()
    {
        await using var factory = new AdminAppFactory(_ => Status(HttpStatusCode.Unauthorized));

        using var response = await GetAsAsync(factory, "/rules/ORD-001?x=1");

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location;
        Assert.IsNotNull(location);
        StringAssert.StartsWith(location.GetLeftPart(UriPartial.Path), AdminAppFactory.AuthorizeEndpoint);
        Assert.AreEqual("/rules/ORD-001?x=1", factory.ReturnUrlOf(location));
        Assert.IsTrue(response.Headers.GetValues("Set-Cookie").Any(c => c.StartsWith(".eShopAdmin.Reauth=1", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExpiredTokenOnTheRulesListAlsoReauthenticates()
    {
        await using var factory = new AdminAppFactory(_ => Status(HttpStatusCode.Unauthorized));

        using var response = await GetAsAsync(factory, "/rules");

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.AreEqual("/rules", factory.ReturnUrlOf(response.Headers.Location!));
    }

    [TestMethod]
    public async Task ASecondUnauthorizedInsideTheWindowShowsTheErrorPageInsteadOfLooping()
    {
        await using var factory = new AdminAppFactory(_ => Status(HttpStatusCode.Unauthorized));

        using var response = await GetAsAsync(factory, "/rules", cookie: ".eShopAdmin.Reauth=1");

        Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(body, ErrorCopy);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.InternalServerError)]
    [DataRow(HttpStatusCode.Forbidden)]
    [DataRow(HttpStatusCode.ServiceUnavailable)]
    [DataRow(HttpStatusCode.BadGateway)]
    public async Task AnApiFailureReachesTheErrorPage(HttpStatusCode apiStatus)
    {
        await using var factory = new AdminAppFactory(_ => Status(apiStatus));

        using var response = await GetAsAsync(factory, "/rules");

        Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(body, "<title>Something went wrong");
        StringAssert.Contains(body, "<h1>Something went wrong</h1>");
        StringAssert.Contains(body, ErrorCopy);
        StringAssert.Contains(body, "href=\"rules\"");
        DoesNotLeakDetails(body);
    }

    [TestMethod]
    public async Task AnUnreachableApiReachesTheErrorPage()
    {
        await using var factory = new AdminAppFactory(_ => throw new HttpRequestException("rules.internal:5432 refused the connection"));

        using var response = await GetAsAsync(factory, "/rules");

        Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(body, ErrorCopy);
        DoesNotLeakDetails(body);
    }

    [TestMethod]
    public async Task TheErrorPageIsReachableWithoutSigningIn()
    {
        await using var factory = new AdminAppFactory(_ => Status(HttpStatusCode.OK));

        using var response = await GetAsAsync(factory, "/error", role: null);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNull(response.Headers.Location);
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), ErrorCopy);
    }

    [TestMethod]
    public async Task AnonymousVisitorsAreSentToSignIn()
    {
        await using var factory = new AdminAppFactory(_ => Status(HttpStatusCode.OK));

        using var response = await GetAsAsync(factory, "/rules", role: null);

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        StringAssert.StartsWith(response.Headers.Location!.GetLeftPart(UriPartial.Path), AdminAppFactory.AuthorizeEndpoint);
        Assert.AreEqual("/rules", factory.ReturnUrlOf(response.Headers.Location!));
    }

    [TestMethod]
    [DataRow("/rules/ORD-001?x=1", "/rules/ORD-001?x=1")]
    [DataRow("//evil.example", "/")]
    [DataRow("/\\evil.example", "/")]
    [DataRow("https://evil.example/", "/")]
    [DataRow("", "/")]
    public async Task LoginOnlyReturnsToLocalUrls(string returnUrl, string expected)
    {
        await using var factory = new AdminAppFactory(_ => Status(HttpStatusCode.OK));

        using var response = await GetAsAsync(factory, $"/authentication/login?returnUrl={Uri.EscapeDataString(returnUrl)}", role: null);

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.AreEqual(expected, factory.ReturnUrlOf(response.Headers.Location!));
    }

    [TestMethod]
    public async Task AnUnknownRuleStillRendersTheNotFoundPage()
    {
        await using var factory = new AdminAppFactory(request => Json(
            request.RequestUri!.AbsolutePath.EndsWith("/types", StringComparison.Ordinal) ? RuleTypesJson : "[]"));

        using var response = await GetAsAsync(factory, "/rules/NOPE-1");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), "<h1>Page not found</h1>");
    }

    [TestMethod]
    public async Task AFailedPostReachesTheErrorPage()
    {
        await using var factory = new AdminAppFactory(request => request.Method == HttpMethod.Post
            ? Status(HttpStatusCode.InternalServerError)
            : Json(request.RequestUri!.AbsolutePath.EndsWith("/types", StringComparison.Ordinal) ? RuleTypesJson : "[]"));
        using var client = factory.CreateAppClient();

        using var form = new HttpRequestMessage(HttpMethod.Get, "/rules/ORD-001/drafts/new");
        form.Headers.Add(AdminAppFactory.RoleHeader, "RulesEditor");
        using var formResponse = await client.SendAsync(form);
        Assert.AreEqual(HttpStatusCode.OK, formResponse.StatusCode);
        var html = await formResponse.Content.ReadAsStringAsync();

        var token = HiddenValue(html, "__RequestVerificationToken");
        var handler = HiddenValue(html, "_handler");
        var antiforgeryCookie = formResponse.Headers.GetValues("Set-Cookie").First(c => c.StartsWith(".AspNetCore.Antiforgery", StringComparison.Ordinal)).Split(';')[0];

        using var post = new HttpRequestMessage(HttpMethod.Post, "/rules/ORD-001/drafts/new")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["_handler"] = handler,
                ["intent"] = "save",
                ["Input.GracePeriod.Minutes"] = "5",
                ["Input.EffectiveFromUtc"] = "2099-01-01T09:00",
            }),
        };
        post.Headers.Add(AdminAppFactory.RoleHeader, "RulesEditor");
        post.Headers.Add("Cookie", antiforgeryCookie);
        using var response = await client.SendAsync(post);

        Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(body, ErrorCopy);
        DoesNotLeakDetails(body);
    }

    private const string RuleTypesJson = """
        [{"id":"ORD-001","name":"Order grace period","affectsPrice":false,"targeting":"Global","default":{"minutes":1},"parameterSchema":{}}]
        """;

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/json")),
    };

    private static string HiddenValue(string html, string name)
    {
        var match = System.Text.RegularExpressions.Regex.Match(html, $"name=\"{name}\"[^>]*value=\"([^\"]*)\"");
        if (!match.Success)
        {
            match = System.Text.RegularExpressions.Regex.Match(html, $"value=\"([^\"]*)\"[^>]*name=\"{name}\"");
        }

        Assert.IsTrue(match.Success, $"The form has no {name} field.");
        return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static void DoesNotLeakDetails(string body)
    {
        foreach (var leak in new[] { "HttpRequestException", "Exception", "StackTrace", " at eShopAdmin", "rules.internal", "unexpected status" })
        {
            Assert.DoesNotContain(leak, body, StringComparison.OrdinalIgnoreCase);
        }
    }
}
