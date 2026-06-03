using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Controllers;

[Collection("Database")]
public sealed class DeveloperControllerTests(DatabaseFixture db)
{
    [Fact]
    public async Task GetVisitsStats_AsDeveloper_Returns200WithStats()
    {
        var client = db.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthHelper.GenerateDeveloperJwt());

        var resp = await client.GetAsync("/v1/developer/visits/stats");
        if (resp.StatusCode != HttpStatusCode.OK)
        {
            var content = await resp.Content.ReadAsStringAsync();
            throw new Exception($"Request failed with status {resp.StatusCode}. Body: {content}");
        }
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("totalPageViews", out _).Should().BeTrue();
        body.TryGetProperty("uniqueVisitors", out _).Should().BeTrue();
        body.TryGetProperty("pageViewsToday", out _).Should().BeTrue();
        body.TryGetProperty("pageViewsYesterday", out _).Should().BeTrue();
        body.TryGetProperty("visitsByDate", out _).Should().BeTrue();
        body.TryGetProperty("visitsByBrowser", out _).Should().BeTrue();
        body.TryGetProperty("visitsByPortal", out _).Should().BeTrue();
        body.TryGetProperty("visitsByOs", out _).Should().BeTrue();
    }
}
