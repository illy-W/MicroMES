using System.Net;
using System.Text;
using FluentAssertions;
using MicroMES.ApiFeatures.VersionFeature.Services;
using MicroMES.ApiTests.Shared.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace MicroMES.ApiTests.Features.VersionFeature;

public class VersionServiceTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public HttpRequestMessage? Last { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Last = request;
            return Task.FromResult(respond(request));
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static string Releases(params (string Tag, bool Pre)[] items)
        => "[" + string.Join(",", items.Select(i => $"{{\"tag_name\":\"{i.Tag}\",\"prerelease\":{i.Pre.ToString().ToLowerInvariant()},\"draft\":false}}")) + "]";

    private static (VersionService Service, FakeHandler Handler, FakeTimeProvider Clock) Create(
        string current, Func<HttpRequestMessage, HttpResponseMessage> respond, bool? check = null)
    {
        var handler = new FakeHandler(respond);
        var clock = new FakeTimeProvider();
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            check is null ? [] : new Dictionary<string, string?> { ["Updates:Check"] = check.ToString() }).Build();
        return (new VersionService(new BuildInfo(current, "abc123", "2026-01-01"), new Factory(handler), config, clock,
            NullLogger<VersionService>.Instance), handler, clock);
    }

    [Fact]
    public async Task GetVersion_NewerRelease_Success_UpdateAvailable()
    {
        var (service, handler, _) = Create("1.2.0", _ => Json(Releases(("app-v1.3.0", false), ("app-v1.2.0", false), ("landing-v9.0.0", false))));
        var m = (await service.GetVersionAsync()).Data!;
        m.Version.Should().Be("1.2.0");
        m.Commit.Should().Be("abc123");
        m.LatestVersion.Should().Be("1.3.0");
        m.UpdateAvailable.Should().BeTrue();
        m.UpdateCheck.Should().Be("ok");
        handler.Last!.RequestUri!.ToString().Should().Contain("/repos/illy-W/MicroMES/releases");
    }

    [Fact]
    public async Task GetVersion_SameRelease_Success_NoUpdate()
    {
        var (service, _, _) = Create("1.3.0", _ => Json(Releases(("app-v1.3.0", false))));
        var m = (await service.GetVersionAsync()).Data!;
        m.UpdateAvailable.Should().BeFalse();
        m.UpdateCheck.Should().Be("ok");
    }

    [Fact]
    public async Task GetVersion_OlderRelease_Success_NoUpdate()
    {
        var (service, _, _) = Create("2.0.0", _ => Json(Releases(("app-v1.3.0", false))));
        (await service.GetVersionAsync()).Data!.UpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task GetVersion_OnlyPreReleaseNewer_Success_IgnoresPreRelease()
    {
        var (service, _, _) = Create("1.2.0", _ => Json(Releases(("app-v2.0.0-rc.1", true), ("app-v1.2.0", false))));
        var m = (await service.GetVersionAsync()).Data!;
        m.LatestVersion.Should().Be("1.2.0");
        m.UpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task GetVersion_DevelopmentBuild_Success_UpdateAvailableForAnyRelease()
    {
        var (service, _, _) = Create("0.0.0-dev", _ => Json(Releases(("app-v0.1.0", false))));
        (await service.GetVersionAsync()).Data!.UpdateAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task GetVersion_NoAppRelease_Success_Unknown()
    {
        var (service, _, _) = Create("1.0.0", _ => Json(Releases(("landing-v1.0.0", false))));
        var m = (await service.GetVersionAsync()).Data!;
        m.UpdateCheck.Should().Be("unknown");
        m.LatestVersion.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetVersion_HttpError_Success_UnknownWithoutFailing(HttpStatusCode status)
    {
        var (service, _, _) = Create("1.0.0", _ => Json("{}", status));
        var result = await service.GetVersionAsync();
        result.IsSuccessful.Should().BeTrue();
        result.Data!.UpdateCheck.Should().Be("unknown");
        result.Data.UpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task GetVersion_NetworkException_Success_UnknownWithoutFailing()
    {
        var (service, _, _) = Create("1.0.0", _ => throw new HttpRequestException("offline"));
        var result = await service.GetVersionAsync();
        result.IsSuccessful.Should().BeTrue();
        result.Data!.UpdateCheck.Should().Be("unknown");
    }

    [Fact]
    public async Task GetVersion_InvalidJson_Success_Unknown()
    {
        var (service, _, _) = Create("1.0.0", _ => Json("not json"));
        (await service.GetVersionAsync()).Data!.UpdateCheck.Should().Be("unknown");
    }

    [Fact]
    public async Task GetVersion_CheckDisabled_Success_NoNetworkCall()
    {
        var (service, handler, _) = Create("1.0.0", _ => Json(Releases(("app-v9.0.0", false))), check: false);
        var m = (await service.GetVersionAsync()).Data!;
        m.UpdateCheck.Should().Be("disabled");
        m.LatestVersion.Should().BeNull();
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task GetVersion_RepeatedCalls_Success_CachedFor24Hours()
    {
        var (service, handler, clock) = Create("1.0.0", _ => Json(Releases(("app-v1.1.0", false))));
        await service.GetVersionAsync();
        clock.Advance(TimeSpan.FromHours(23));
        await service.GetVersionAsync();
        handler.Calls.Should().Be(1);

        clock.Advance(TimeSpan.FromHours(2));
        await service.GetVersionAsync();
        handler.Calls.Should().Be(2);
    }

    [Theory]
    [InlineData("1.0.0", "1.0.1", -1)]
    [InlineData("1.10.0", "1.9.0", 1)]
    [InlineData("1.0.0-rc.1", "1.0.0", -1)]
    [InlineData("1.0.0-rc.2", "1.0.0-rc.10", -1)]
    [InlineData("1.0.0+abc", "1.0.0", 0)]
    public void SemanticVersion_Compare_Success_FollowsSemverPrecedence(string a, string b, int expected)
        => Math.Sign(SemanticVersion.TryParse(a)!.CompareTo(SemanticVersion.TryParse(b))).Should().Be(expected);
}
