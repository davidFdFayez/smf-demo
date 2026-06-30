using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SMF.Application.Features.Members.Commands.RegisterMember;
using SMF.Application.Features.Members.Queries.GetMembers;
using SMF.Domain.Enums;
using Xunit;

namespace SMF.IntegrationTests;

public class MemberRegistrationTests : IClassFixture<SmfWebApplicationFactory>
{
    private readonly SmfWebApplicationFactory _factory;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public MemberRegistrationTests(SmfWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_AdultAthlete_Returns201_WithFormattedSmfId()
    {
        using var client = _factory.CreateClient();

        var payload = BuildPayload(
            fullName: "Sara Al-Ahmed",
            dateOfBirth: "1998-03-12",
            role: "Athlete",
            guardianConsent: false);

        var response = await client.PostAsJsonAsync("/api/members", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<RegisterMemberResult>(JsonOptions);
        result.Should().NotBeNull();
        result!.Id.Should().NotBeEmpty();
        result.RegistrationStatus.Should().Be(RegistrationStatus.Pending);
        result.SMF_ID.Should().MatchRegex(@"^#SMF\d{4}-\d{5}$");
    }

    [Fact]
    public async Task Register_MinorAthlete_WithoutConsent_Returns400_WithGuardianConsentError()
    {
        using var client = _factory.CreateClient();

        var minorDob = DateTime.UtcNow.AddYears(-12).ToString("yyyy-MM-dd");

        var payload = BuildPayload(
            fullName: "Yazeed Al-Mutairi",
            dateOfBirth: minorDob,
            role: "Athlete",
            guardianConsent: false);

        var response = await client.PostAsJsonAsync("/api/members", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemShape>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Errors.Should().ContainKey("GuardianConsent");
        problem.Errors["GuardianConsent"].Should()
            .Contain(msg => msg.Contains("guardian", StringComparison.OrdinalIgnoreCase)
                         && msg.Contains("consent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Register_MinorAthlete_WithConsent_Returns201()
    {
        using var client = _factory.CreateClient();

        var minorDob = DateTime.UtcNow.AddYears(-10).ToString("yyyy-MM-dd");

        var payload = BuildPayload(
            fullName: "Layla Al-Fahad",
            dateOfBirth: minorDob,
            role: "Athlete",
            guardianConsent: true);

        var response = await client.PostAsJsonAsync("/api/members", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Register_MinorCoach_WithoutConsent_Returns201_RuleOnlyAppliesToAthletes()
    {
        using var client = _factory.CreateClient();

        // Young coach is silly but our rule is specific to Athletes — verify the rule isn't over-applied.
        var minorDob = DateTime.UtcNow.AddYears(-17).ToString("yyyy-MM-dd");

        var payload = BuildPayload(
            fullName: "Omar Al-Youngish",
            dateOfBirth: minorDob,
            role: "Coach",
            guardianConsent: false,
            licenseLevel: "Assistant");

        var response = await client.PostAsJsonAsync("/api/members", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Register_Sequential_AssignsIncrementingSequence_ForSameYear()
    {
        using var client = _factory.CreateClient();

        var first = await RegisterAdultAthlete(client, "First Member");
        var second = await RegisterAdultAthlete(client, "Second Member");

        var parseSeq = (string id) => int.Parse(id.Split('-')[1]);

        parseSeq(second.SMF_ID).Should().Be(parseSeq(first.SMF_ID) + 1);
    }

    [Fact]
    public async Task Approve_PendingMember_Returns204_AndFlipsStatus()
    {
        using var client = _factory.CreateClient();

        var registered = await RegisterAdultAthlete(client, "Approvable Member");

        var approveResponse = await client.PostAsync($"/api/members/{registered.Id}/approve", content: null);
        approveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await client.GetFromJsonAsync<PagedResult<MemberListItem>>(
            "/api/members?page=1&pageSize=50", JsonOptions);

        list.Should().NotBeNull();
        var approved = list!.Items.Single(m => m.Id == registered.Id);
        approved.RegistrationStatus.Should().Be(RegistrationStatus.Approved);
    }

    [Fact]
    public async Task Approve_UnknownMember_Returns404()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync($"/api/members/{Guid.NewGuid()}/approve", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListMembers_Returns_RegisteredMembers_AndHonorsPaging()
    {
        using var client = _factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            await RegisterAdultAthlete(client, $"List Member {i}");
        }

        var firstPage = await client.GetFromJsonAsync<PagedResult<MemberListItem>>(
            "/api/members?page=1&pageSize=2", JsonOptions);

        firstPage.Should().NotBeNull();
        firstPage!.Items.Should().HaveCount(2);
        firstPage.TotalCount.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task Register_MissingCompliance_Returns400_ForEachUnacceptedPolicy()
    {
        using var client = _factory.CreateClient();

        var payload = BuildPayload(
            fullName: "Non Compliant",
            dateOfBirth: "1990-01-01",
            role: "Athlete",
            guardianConsent: false,
            acceptTerms: false,
            acceptPrivacyPolicy: false,
            acceptCodeOfConduct: false);

        var response = await client.PostAsJsonAsync("/api/members", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemShape>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Errors.Should().ContainKeys("AcceptTerms", "AcceptPrivacyPolicy", "AcceptCodeOfConduct");
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns400_WithEmailError()
    {
        using var client = _factory.CreateClient();

        var payload = BuildPayload(
            fullName: "Bad Email",
            dateOfBirth: "1990-01-01",
            role: "Athlete",
            guardianConsent: false,
            email: "not-an-email");

        var response = await client.PostAsJsonAsync("/api/members", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemShape>(JsonOptions);
        problem!.Errors.Should().ContainKey("Email");
    }

    private static async Task<RegisterMemberResult> RegisterAdultAthlete(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/members",
            BuildPayload(
                fullName: name,
                dateOfBirth: "1995-06-01",
                role: "Athlete",
                guardianConsent: false));

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<RegisterMemberResult>(JsonOptions);
        return result!;
    }

    private static object BuildPayload(
        string fullName,
        string dateOfBirth,
        string role,
        bool guardianConsent,
        string? email = null,
        string? phoneNumber = null,
        string? nationalId = null,
        bool acceptTerms = true,
        bool acceptPrivacyPolicy = true,
        bool acceptCodeOfConduct = true,
        string? licenseLevel = null,
        Guid? affiliatedClubId = null,
        int? yearsOfExperience = null)
    {
        // Derive a unique-ish email / national-id per caller so list tests
        // that register several members in a row don't collide on anything
        // the domain might later treat as a unique key.
        var slug = Guid.NewGuid().ToString("N")[..8];

        return new
        {
            fullName,
            dateOfBirth,
            role,
            guardianConsent,
            email = email ?? $"test-{slug}@smf.local",
            phoneNumber = phoneNumber ?? "+966500000000",
            nationalId = nationalId ?? $"TEST-{slug}",
            acceptTerms,
            acceptPrivacyPolicy,
            acceptCodeOfConduct,
            licenseLevel,
            affiliatedClubId,
            yearsOfExperience,
        };
    }

    private sealed record ValidationProblemShape(
        string? Title,
        int? Status,
        Dictionary<string, string[]> Errors);
}
