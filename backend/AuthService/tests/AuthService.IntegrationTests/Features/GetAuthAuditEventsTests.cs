using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AuthService.Contracts.Requests;
using AuthService.Contracts.Responses;
using AuthService.Domain.Identity;
using AuthService.Infrastructure.Postgres;
using AuthService.Infrastructure.Postgres.Seeding;
using AuthService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedService.Core.Abstractions;
using SharedService.SharedKernel;

namespace AuthService.IntegrationTests.Features;

public sealed class GetAuthAuditEventsTests : AuthServiceBaseTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public GetAuthAuditEventsTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetAuthAuditEvents_By_CompanyAdmin_Should_Return_Only_Current_Company_Events()
    {
        Guid companyId = Guid.NewGuid();
        Guid anotherCompanyId = Guid.NewGuid();

        ApplicationUser admin = await CreateIdentityUserAsync(
            "audit-read-admin@example.com",
            "auditreadadmin",
            "Audit Read Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);
        ApplicationUser targetUser = await CreateIdentityUserAsync(
            "audit-read-user@example.com",
            "auditreaduser",
            "Audit Read User",
            companyId,
            AuthRoles.VIEWER);
        ApplicationUser anotherCompanyUser = await CreateIdentityUserAsync(
            "audit-read-other@example.com",
            "auditreadother",
            "Audit Read Other",
            anotherCompanyId,
            AuthRoles.VIEWER);

        await AddAuditEventAsync(
            companyId,
            targetUser.Id,
            targetUser.Email,
            AuthAuditActions.USER_ROLE_CHANGED,
            admin.Id);
        await AddAuditEventAsync(
            anotherCompanyId,
            anotherCompanyUser.Id,
            anotherCompanyUser.Email,
            AuthAuditActions.USER_ROLE_CHANGED,
            admin.Id);

        TokenResponse login = await LoginAsync("audit-read-admin@example.com");
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"/api/auth/audit-events?page=1&pageSize=20&action={AuthAuditActions.USER_ROLE_CHANGED}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PagedList<AuthAuditEventResponse> auditEventsPage = await ReadAuditEventsAsync(response);
        auditEventsPage.TotalCount.Should().Be(1);
        auditEventsPage.Items.Should().ContainSingle();
        auditEventsPage.Items.Single().CompanyId.Should().Be(companyId);
        auditEventsPage.Items.Single().UserId.Should().Be(targetUser.Id);
    }

    [Fact]
    public async Task GetAuthAuditEvents_By_SystemAdmin_Should_Filter_By_Company_And_Page()
    {
        Guid systemCompanyId = Guid.NewGuid();
        Guid firstCompanyId = Guid.NewGuid();
        Guid secondCompanyId = Guid.NewGuid();

        await CreateIdentityUserAsync(
            "audit-read-system@example.com",
            "auditreadsystem",
            "Audit Read System",
            systemCompanyId,
            AuthRoles.SYSTEM_ADMIN);
        ApplicationUser firstUser = await CreateIdentityUserAsync(
            "audit-read-first@example.com",
            "auditreadfirst",
            "Audit Read First",
            firstCompanyId,
            AuthRoles.VIEWER);
        ApplicationUser secondUser = await CreateIdentityUserAsync(
            "audit-read-second@example.com",
            "auditreadsecond",
            "Audit Read Second",
            secondCompanyId,
            AuthRoles.VIEWER);

        await AddAuditEventAsync(
            firstCompanyId,
            firstUser.Id,
            firstUser.Email,
            AuthAuditActions.INVITE_CREATED);
        await AddAuditEventAsync(
            firstCompanyId,
            firstUser.Id,
            firstUser.Email,
            AuthAuditActions.USER_STATUS_CHANGED);
        await AddAuditEventAsync(
            secondCompanyId,
            secondUser.Id,
            secondUser.Email,
            AuthAuditActions.INVITE_CREATED);

        TokenResponse login = await LoginAsync("audit-read-system@example.com");
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"/api/auth/audit-events?companyId={firstCompanyId}&page=2&pageSize=1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PagedList<AuthAuditEventResponse> auditEventsPage = await ReadAuditEventsAsync(response);
        auditEventsPage.Page.Should().Be(2);
        auditEventsPage.PageSize.Should().Be(1);
        auditEventsPage.TotalCount.Should().Be(2);
        auditEventsPage.Items.Should().ContainSingle();
        auditEventsPage.Items.Should().OnlyContain(auditEvent => auditEvent.CompanyId == firstCompanyId);
        auditEventsPage.HasPreviousPage.Should().BeTrue();
        auditEventsPage.HasNextpage.Should().BeFalse();
    }

    [Fact]
    public async Task GetAuthAuditEvents_Should_Not_Return_Sensitive_Audit_Columns()
    {
        Guid companyId = Guid.NewGuid();
        ApplicationUser admin = await CreateIdentityUserAsync(
            "audit-read-safe-admin@example.com",
            "auditreadsafeadmin",
            "Audit Read Safe Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);

        await AddAuditEventAsync(
            companyId,
            admin.Id,
            admin.Email,
            AuthAuditActions.LOGOUT,
            admin.Id);

        TokenResponse login = await LoginAsync("audit-read-safe-admin@example.com");
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/auth/audit-events?page=1&pageSize=20");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string json = await response.Content.ReadAsStringAsync();
        json.Contains("metadataJson", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        json.Contains("ipAddress", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        json.Contains("userAgent", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        json.Contains("secret-token", StringComparison.Ordinal).Should().BeFalse();

        PagedList<AuthAuditEventResponse> auditEventsPage = ReadAuditEvents(json);
        auditEventsPage.Items.Should().ContainSingle();
        auditEventsPage.Items.Single().Email.Should().Be("audit-read-safe-admin@example.com");
    }

    [Fact]
    public async Task GetAuthAuditEvents_Without_Access_Token_Should_Return_Unauthorized()
    {
        HttpResponseMessage response = await AppHttpClient.GetAsync("/api/auth/audit-events?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAuthAuditEvents_Without_UsersManage_Permission_Should_Return_Forbidden()
    {
        Guid companyId = Guid.NewGuid();
        await CreateIdentityUserAsync(
            "audit-read-viewer@example.com",
            "auditreadviewer",
            "Audit Read Viewer",
            companyId,
            AuthRoles.VIEWER);

        TokenResponse login = await LoginAsync("audit-read-viewer@example.com");
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/auth/audit-events?page=1&pageSize=20");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAuthAuditEvents_With_Date_Filter_Should_Return_Matching_Events()
    {
        Guid companyId = Guid.NewGuid();
        ApplicationUser admin = await CreateIdentityUserAsync(
            "audit-read-date-admin@example.com",
            "auditreaddateadmin",
            "Audit Read Date Admin",
            companyId,
            AuthRoles.COMPANY_ADMIN);

        await AddAuditEventAsync(
            companyId,
            admin.Id,
            admin.Email,
            AuthAuditActions.LOGOUT,
            admin.Id);

        string createdFromUtc = Uri.EscapeDataString(DateTime.UtcNow.AddMinutes(-5).ToString("O"));
        string createdToUtc = Uri.EscapeDataString(DateTime.UtcNow.AddMinutes(5).ToString("O"));

        TokenResponse login = await LoginAsync("audit-read-date-admin@example.com");
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"/api/auth/audit-events?page=1&pageSize=20&createdFromUtc={createdFromUtc}&createdToUtc={createdToUtc}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PagedList<AuthAuditEventResponse> auditEventsPage = await ReadAuditEventsAsync(response);
        auditEventsPage.Items.Should().Contain(auditEvent => auditEvent.Action == AuthAuditActions.LOGOUT);
    }

    private static async Task<PagedList<AuthAuditEventResponse>> ReadAuditEventsAsync(HttpResponseMessage response)
    {
        Envelope<PagedList<AuthAuditEventResponse>>? envelope =
            await response.Content.ReadFromJsonAsync<Envelope<PagedList<AuthAuditEventResponse>>>();

        envelope.Should().NotBeNull();
        envelope!.Result.Should().NotBeNull();

        return envelope.Result!;
    }

    private static PagedList<AuthAuditEventResponse> ReadAuditEvents(string json)
    {
        Envelope<PagedList<AuthAuditEventResponse>>? envelope =
            JsonSerializer.Deserialize<Envelope<PagedList<AuthAuditEventResponse>>>(json, JsonOptions);

        envelope.Should().NotBeNull();
        envelope!.Result.Should().NotBeNull();

        return envelope.Result!;
    }

    private Task<bool> AddAuditEventAsync(
        Guid companyId,
        Guid userId,
        string? email,
        string action,
        Guid? actorUserId = null)
    {
        AuthAuditEvent auditEvent = AuthAuditEvent
            .Create(
                companyId,
                userId,
                email,
                action,
                actorUserId,
                "127.0.0.1",
                "integration-test-agent",
                """{"token":"secret-token"}""")
            .Value;

        return ExecuteInDb(async dbContext =>
        {
            dbContext.AuthAuditEvents.Add(auditEvent);
            await dbContext.SaveChangesAsync();

            return true;
        });
    }

    private async Task<TokenResponse> LoginAsync(string email)
    {
        HttpResponseMessage response = await AppHttpClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "password123"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        Envelope<TokenResponse>? envelope = await response.Content.ReadFromJsonAsync<Envelope<TokenResponse>>();
        envelope.Should().NotBeNull();
        envelope!.Result.Should().NotBeNull();

        return envelope.Result!;
    }

    private async Task<ApplicationUser> CreateIdentityUserAsync(
        string email,
        string username,
        string displayName,
        Guid companyId,
        string role)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();

        AuthIdentitySeeder seeder = scope.ServiceProvider.GetRequiredService<AuthIdentitySeeder>();
        await seeder.SeedAsync();

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = new(
            email,
            Username.Create(username).Value,
            DisplayName.Create(displayName).Value,
            companyId);

        IdentityResult createResult = await userManager.CreateAsync(user, "password123");
        createResult.Succeeded.Should().BeTrue(string.Join("; ", createResult.Errors.Select(error => error.Description)));

        IdentityResult roleResult = await userManager.AddToRoleAsync(user, role);
        roleResult.Succeeded.Should().BeTrue(string.Join("; ", roleResult.Errors.Select(error => error.Description)));

        return user;
    }
}
