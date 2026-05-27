using System.Net;
using System.Text.Json;
using BookBorrowingSystem;
using BookBorrowingSystem.Data;
using LibraryApplication.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestExamIS.Tests.Utils;

namespace TestExamIS.Tests.ControllersTests;

[Collection("Test Suite")]
public class MemberControllerTests : LoggedTestBase, IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public MemberControllerTests(WebApplicationFactory<Program> factory, GlobalTestFixture fixture) : base(fixture)
    {
        _factory = factory.WithTestDatabase();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false,
        });
        _client.Timeout = TimeSpan.FromSeconds(10);
    }

    // ── Req 3: Validation ──────────────────────────────────────

    [LoggedFact(Category = "MemberController", Points = 3)]
    public async Task Create_MissingFirstName_ReturnsView()
    {
        await RunTestAsync(async () =>
        {
            var initialCount = await TestDatabaseHelper.GetCount<Member>(_factory.Services);

            var getResponse = await _client.GetAsync("/Member/Create");
            var antiForgeryToken = await getResponse.GetAntiForgeryTokenAsync();

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("FirstName", ""), // Required
                new KeyValuePair<string, string>("LastName", "Петровски"),
                new KeyValuePair<string, string>("Email", "test@example.com"),
                new KeyValuePair<string, string>("PhoneNumber", "070111222"),
                new KeyValuePair<string, string>("MembershipDate", "2024-01-01"),
                new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken)
            });

            var response = await _client.PostAsync("/Member/Create", formContent);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var newCount = await TestDatabaseHelper.GetCount<Member>(_factory.Services);
            Assert.Equal(initialCount, newCount);
        });
    }

    [LoggedFact(Category = "MemberController", Points = 3)]
    public async Task Create_MissingLastName_ReturnsView()
    {
        await RunTestAsync(async () =>
        {
            var initialCount = await TestDatabaseHelper.GetCount<Member>(_factory.Services);

            var getResponse = await _client.GetAsync("/Member/Create");
            var antiForgeryToken = await getResponse.GetAntiForgeryTokenAsync();

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("FirstName", "Александар"),
                new KeyValuePair<string, string>("LastName", ""), // Required
                new KeyValuePair<string, string>("Email", "test@example.com"),
                new KeyValuePair<string, string>("PhoneNumber", "070111222"),
                new KeyValuePair<string, string>("MembershipDate", "2024-01-01"),
                new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken)
            });

            var response = await _client.PostAsync("/Member/Create", formContent);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var newCount = await TestDatabaseHelper.GetCount<Member>(_factory.Services);
            Assert.Equal(initialCount, newCount);
        });
    }

    [LoggedFact(Category = "MemberController", Points = 5)]
    public async Task Create_InvalidPhoneNumber_ReturnsView()
    {
        await RunTestAsync(async () =>
        {
            var initialCount = await TestDatabaseHelper.GetCount<Member>(_factory.Services);

            var getResponse = await _client.GetAsync("/Member/Create");
            var antiForgeryToken = await getResponse.GetAntiForgeryTokenAsync();

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("FirstName", "Александар"),
                new KeyValuePair<string, string>("LastName", "Петровски"),
                new KeyValuePair<string, string>("Email", "test@example.com"),
                new KeyValuePair<string, string>("PhoneNumber", "123"), // Not 9 digits
                new KeyValuePair<string, string>("MembershipDate", "2024-01-01"),
                new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken)
            });

            var response = await _client.PostAsync("/Member/Create", formContent);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var newCount = await TestDatabaseHelper.GetCount<Member>(_factory.Services);
            Assert.Equal(initialCount, newCount);
        });
    }

    // ── CRUD: Create ───────────────────────────────────────────

    [LoggedFact(Category = "MemberController", Points = 1)]
    public async Task Create_ValidMember_RedirectsToIndex()
    {
        await RunTestAsync(async () =>
        {
            var initialCount = await TestDatabaseHelper.GetCount<Member>(_factory.Services);

            var getResponse = await _client.GetAsync("/Member/Create");
            var antiForgeryToken = await getResponse.GetAntiForgeryTokenAsync();

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("FirstName", "Тест"),
                new KeyValuePair<string, string>("LastName", "Корисник"),
                new KeyValuePair<string, string>("Email", "test@example.com"),
                new KeyValuePair<string, string>("PhoneNumber", "070111222"),
                new KeyValuePair<string, string>("MembershipDate", "2024-01-01"),
                new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken)
            });

            var response = await _client.PostAsync("/Member/Create", formContent);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Member", response.Headers.Location?.ToString());

            var newCount = await TestDatabaseHelper.GetCount<Member>(_factory.Services);
            Assert.Equal(initialCount + 1, newCount);
        });
    }

    // ── CRUD: Details ──────────────────────────────────────────

    [LoggedFact(Category = "MemberController", Points = 1)]
    public async Task Details_ValidId_ReturnsMember()
    {
        await RunTestAsync(async () =>
        {
            var member = await TestDatabaseHelper.GetFirst<Member>(_factory.Services);

            var response = await _client.GetAsync($"/Member/Details/{member.Id}");

            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains(member.FirstName, content);
        });
    }

    [LoggedFact(Category = "MemberController", Points = 1)]
    public async Task Details_InvalidId_ReturnsNotFound()
    {
        await RunTestAsync(async () =>
        {
            var response = await _client.GetAsync("/Member/Details/99999");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });
    }

    // ── CRUD: Edit ─────────────────────────────────────────────

    [LoggedFact(Category = "MemberController", Points = 1)]
    public async Task Edit_ValidMember_RedirectsToIndex()
    {
        await RunTestAsync(async () =>
        {
            var member = await TestDatabaseHelper.GetFirst<Member>(_factory.Services);

            var getResponse = await _client.GetAsync($"/Member/Edit/{member.Id}");
            var antiForgeryToken = await getResponse.GetAntiForgeryTokenAsync();

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Id", member.Id.ToString()),
                new KeyValuePair<string, string>("FirstName", member.FirstName + " ИЗМ"),
                new KeyValuePair<string, string>("LastName", member.LastName),
                new KeyValuePair<string, string>("Email", member.Email),
                new KeyValuePair<string, string>("PhoneNumber", member.PhoneNumber),
                new KeyValuePair<string, string>("MembershipDate", member.MembershipDate.ToString("yyyy-MM-dd")),
                new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken)
            });

            var response = await _client.PostAsync($"/Member/Edit/{member.Id}", formContent);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Member", response.Headers.Location?.ToString());
        });
    }

    // ── CRUD: Delete ───────────────────────────────────────────

    [LoggedFact(Category = "MemberController", Points = 1)]
    public async Task Delete_ValidMember_RemovesAndRedirects()
    {
        await RunTestAsync(async () =>
        {
            var initialCount = await TestDatabaseHelper.GetCount<Member>(_factory.Services);
            var member = await TestDatabaseHelper.GetFirst<Member>(_factory.Services);

            var getResponse = await _client.GetAsync($"/Member/Delete/{member.Id}");
            var antiForgeryToken = await getResponse.GetAntiForgeryTokenAsync();

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken)
            });

            var response = await _client.PostAsync($"/Member/Delete/{member.Id}", formContent);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            var newCount = await TestDatabaseHelper.GetCount<Member>(_factory.Services);
            Assert.Equal(initialCount - 1, newCount);
        });
    }

    // ── Req 7d: ReturnBook ─────────────────────────────────────

    [LoggedFact(Category = "MemberController", Points = 5)]
    public async Task ReturnBook_SetsReturnDateAndRedirectsToMemberDetails()
    {
        await RunTestAsync(async () =>
        {
            // Get an active borrowing (ReturnDate == null)
            var borrowing = await GetActiveBorrowingAsync();
            Assert.NotNull(borrowing);

            var response = await _client.PostAsync($"/Member/ReturnBook/{borrowing.Id}", new StringContent(""));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains($"/Member/Details/{borrowing.MemberId}", response.Headers.Location?.ToString());

            // Verify ReturnDate is now set
            var updated = TestDatabaseHelper.GetById<BookBorrowing>(
                _factory.Services,
                b => b.Id == borrowing.Id);
            Assert.NotNull(updated);
            Assert.NotNull(updated.ReturnDate);
        });
    }

    // ── Req 5: Tabulator page ──────────────────────────────────

    [LoggedFact(Category = "MemberController", Points = 3)]
    public async Task Tabulator_ReturnsView()
    {
        await RunTestAsync(async () =>
        {
            var response = await _client.GetAsync("/Member/Tabulator");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("tabulator-table", content);
        });
    }

    // ── Req 5b: API endpoint returns JSON ─────────────────────

    [LoggedFact(Category = "MemberController", Points = 5)]
    public async Task Api_GetMembers_ReturnsJsonList()
    {
        await RunTestAsync(async () =>
        {
            var response = await _client.GetAsync("/api/MemberApi");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var members = JsonSerializer.Deserialize<JsonElement[]>(content);

            Assert.NotNull(members);
            Assert.True(members.Length > 0);

            // Check expected fields exist
            var first = members[0];
            Assert.True(first.TryGetProperty("firstName", out _) || first.TryGetProperty("FirstName", out _),
                "JSON must contain firstName field");
            Assert.True(first.TryGetProperty("lastName", out _) || first.TryGetProperty("LastName", out _),
                "JSON must contain lastName field");
            Assert.True(first.TryGetProperty("email", out _) || first.TryGetProperty("Email", out _),
                "JSON must contain email field");
        });
    }

    // ── Helpers ────────────────────────────────────────────────

    private async Task<BookBorrowing?> GetActiveBorrowingAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.BookBorrowings
            .Where(b => b.ReturnDate == null)
            .FirstOrDefaultAsync();
    }

    public async Task InitializeAsync() => await TestDatabaseHelper.ResetDatabaseAsync(_factory.Services);
    public async Task DisposeAsync() => await TestDatabaseHelper.ResetDatabaseAsync(_factory.Services);
}
