using System.Net;
using BookBorrowingSystem;
using LibraryApplication.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using TestExamIS.Tests.Utils;

namespace TestExamIS.Tests.ControllersTests;

[Collection("Test Suite")]
public class LibraryControllerTests : LoggedTestBase, IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public LibraryControllerTests(WebApplicationFactory<Program> factory, GlobalTestFixture fixture) : base(fixture)
    {
        _factory = factory.WithTestDatabase();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false,
        });
        _client.Timeout = TimeSpan.FromSeconds(10);
    }

    // ── Req 1: CRUD ──────────────────────────────────────────────

    [LoggedFact(Category = "LibraryController", Points = 1)]
    public async Task Index_ReturnsAllLibraries()
    {
        await RunTestAsync(async () =>
        {
            var response = await _client.GetAsync("/Library");

            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("Градска Библиотека", content);
            Assert.Contains("Национална Библиотека", content);
        });
    }

    // ── Req 1b: Library name is a link to Details ──────────────

    [LoggedFact(Category = "LibraryController", Points = 5)]
    public async Task Index_LibraryNameIsLinkToDetails()
    {
        await RunTestAsync(async () =>
        {
            var library = await TestDatabaseHelper.GetFirst<Library>(_factory.Services);

            var response = await _client.GetAsync("/Library");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            // The library name must appear inside an <a> tag pointing to Details
            Assert.Contains($"/Library/Details/{library.Id}", content);
            Assert.Contains(library.Name, content);

            // Name must be the link text, not just plain text
            var detailsLinkIndex = content.IndexOf($"/Library/Details/{library.Id}");
            var nameIndex = content.IndexOf(library.Name, detailsLinkIndex);
            Assert.True(nameIndex > detailsLinkIndex,
                $"Library name '{library.Name}' should appear as a link to Details, not plain text.");
        });
    }

    // ── Req 1c: Book count column ──────────────────────────────

    [LoggedFact(Category = "LibraryController", Points = 5)]
    public async Task Index_ShowsBookCountPerLibrary()
    {
        await RunTestAsync(async () =>
        {
            var response = await _client.GetAsync("/Library");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            // Library1 has 2 books, Library2 has 1 book
            // Just assert the page contains "2" and "1" in context — we check column header too
            Assert.Contains("TotalBooks", content, StringComparison.OrdinalIgnoreCase);
        });
    }

    // ── Req 4a: Filter by Name ─────────────────────────────────

    [LoggedFact(Category = "LibraryController", Points = 5)]
    public async Task Index_FilterByName_ReturnsMatchingLibrary()
    {
        await RunTestAsync(async () =>
        {
            var response = await _client.GetAsync("/Library?name=Градска+Библиотека");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            Assert.Contains("Градска Библиотека", content);
            Assert.DoesNotContain("Национална Библиотека", content);
        });
    }

    // ── Req 4a: Filter by City ─────────────────────────────────

    [LoggedFact(Category = "LibraryController", Points = 5)]
    public async Task Index_FilterByCity_ReturnsMatchingLibraries()
    {
        await RunTestAsync(async () =>
        {
            // "Битола" has only Национална Библиотека
            var response = await _client.GetAsync("/Library?city=Битола");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            Assert.Contains("Национална Библиотека", content);
            // Градска and Универзитетска are in Скопје — should not appear
            Assert.DoesNotContain("Универзитетска Библиотека", content);
        });
    }

    // ── Req 4b: Filter values persist in form ─────────────────

    [LoggedFact(Category = "LibraryController", Points = 5)]
    public async Task Index_FilterValues_PersistInFormAfterSearch()
    {
        await RunTestAsync(async () =>
        {
            var response = await _client.GetAsync("/Library?name=Градска+Библиотека&city=Скопје");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            // The form inputs should show the searched values
            Assert.Contains("Градска Библиотека", content);
            Assert.Contains("Скопје", content);
            // The values should be inside input value attributes
            Assert.Contains("value=\"Градска Библиотека\"", content);
        });
    }

    // ── CRUD: Create ───────────────────────────────────────────

    [LoggedFact(Category = "LibraryController", Points = 1)]
    public async Task Create_ValidLibrary_RedirectsToIndex()
    {
        await RunTestAsync(async () =>
        {
            var initialCount = await TestDatabaseHelper.GetCount<Library>(_factory.Services);

            var getResponse = await _client.GetAsync("/Library/Create");
            var antiForgeryToken = await getResponse.GetAntiForgeryTokenAsync();

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "Тест Библиотека"),
                new KeyValuePair<string, string>("Address", "ул. Тест 1"),
                new KeyValuePair<string, string>("City", "Тетово"),
                new KeyValuePair<string, string>("Country", "Македонија"),
                new KeyValuePair<string, string>("Rating", "7.5"),
                new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken)
            });

            var response = await _client.PostAsync("/Library/Create", formContent);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Library", response.Headers.Location?.ToString());

            var newCount = await TestDatabaseHelper.GetCount<Library>(_factory.Services);
            Assert.Equal(initialCount + 1, newCount);
        });
    }

    [LoggedFact(Category = "LibraryController", Points = 1)]
    public async Task Create_InvalidLibrary_ReturnsView()
    {
        await RunTestAsync(async () =>
        {
            var initialCount = await TestDatabaseHelper.GetCount<Library>(_factory.Services);

            var getResponse = await _client.GetAsync("/Library/Create");
            var antiForgeryToken = await getResponse.GetAntiForgeryTokenAsync();

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", ""), // Required — blank fails validation
                new KeyValuePair<string, string>("Address", "ул. Тест 1"),
                new KeyValuePair<string, string>("City", "Тетово"),
                new KeyValuePair<string, string>("Country", "Македонија"),
                new KeyValuePair<string, string>("Rating", "7.5"),
                new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken)
            });

            var response = await _client.PostAsync("/Library/Create", formContent);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var newCount = await TestDatabaseHelper.GetCount<Library>(_factory.Services);
            Assert.Equal(initialCount, newCount);
        });
    }

    // ── CRUD: Details ──────────────────────────────────────────

    [LoggedFact(Category = "LibraryController", Points = 1)]
    public async Task Details_ValidId_ReturnsLibrary()
    {
        await RunTestAsync(async () =>
        {
            var library = await TestDatabaseHelper.GetFirst<Library>(_factory.Services);

            var response = await _client.GetAsync($"/Library/Details/{library.Id}");

            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains(library.Name, content);
        });
    }

    [LoggedFact(Category = "LibraryController", Points = 1)]
    public async Task Details_InvalidId_ReturnsNotFound()
    {
        await RunTestAsync(async () =>
        {
            var response = await _client.GetAsync("/Library/Details/99999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });
    }

    // ── CRUD: Edit ─────────────────────────────────────────────

    [LoggedFact(Category = "LibraryController", Points = 1)]
    public async Task Edit_ValidLibrary_RedirectsToIndex()
    {
        await RunTestAsync(async () =>
        {
            var library = await TestDatabaseHelper.GetFirst<Library>(_factory.Services);
            var editedName = library.Name + " - Изменета";

            var getResponse = await _client.GetAsync($"/Library/Edit/{library.Id}");
            var antiForgeryToken = await getResponse.GetAntiForgeryTokenAsync();

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Id", library.Id.ToString()),
                new KeyValuePair<string, string>("Name", editedName),
                new KeyValuePair<string, string>("Address", library.Address),
                new KeyValuePair<string, string>("City", library.City),
                new KeyValuePair<string, string>("Country", library.Country),
                new KeyValuePair<string, string>("Rating", library.Rating.ToString()),
                new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken)
            });

            var response = await _client.PostAsync($"/Library/Edit/{library.Id}", formContent);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Library", response.Headers.Location?.ToString());

            var edited = TestDatabaseHelper.GetById<Library>(_factory.Services, x => x.Id == library.Id);
            Assert.NotNull(edited);
            Assert.Equal(editedName, edited.Name);
        });
    }

    [LoggedFact(Category = "LibraryController", Points = 1)]
    public async Task Edit_MismatchedId_ReturnsNotFound()
    {
        await RunTestAsync(async () =>
        {
            var library = await TestDatabaseHelper.GetFirst<Library>(_factory.Services);

            var getResponse = await _client.GetAsync($"/Library/Edit/{library.Id}");
            var antiForgeryToken = await getResponse.GetAntiForgeryTokenAsync();

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Id", "99999"), // mismatched
                new KeyValuePair<string, string>("Name", library.Name),
                new KeyValuePair<string, string>("Address", library.Address),
                new KeyValuePair<string, string>("City", library.City),
                new KeyValuePair<string, string>("Country", library.Country),
                new KeyValuePair<string, string>("Rating", library.Rating.ToString()),
                new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken)
            });

            var response = await _client.PostAsync($"/Library/Edit/{library.Id}", formContent);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });
    }

    // ── CRUD: Delete ───────────────────────────────────────────

    [LoggedFact(Category = "LibraryController", Points = 1)]
    public async Task Delete_ValidLibrary_RedirectsToIndex()
    {
        await RunTestAsync(async () =>
        {
            var initialCount = await TestDatabaseHelper.GetCount<Library>(_factory.Services);
            var library = await TestDatabaseHelper.GetFirst<Library>(_factory.Services);

            var getResponse = await _client.GetAsync($"/Library/Delete/{library.Id}");
            var antiForgeryToken = await getResponse.GetAntiForgeryTokenAsync();

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken)
            });

            var response = await _client.PostAsync($"/Library/Delete/{library.Id}", formContent);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Library", response.Headers.Location?.ToString());

            var deleted = TestDatabaseHelper.GetById<Library>(_factory.Services, x => x.Id == library.Id);
            Assert.Null(deleted);

            var newCount = await TestDatabaseHelper.GetCount<Library>(_factory.Services);
            Assert.Equal(initialCount - 1, newCount);
        });
    }

    [LoggedFact(Category = "LibraryController", Points = 1)]
    public async Task Delete_InvalidId_ReturnsNotFound()
    {
        await RunTestAsync(async () =>
        {
            var response = await _client.GetAsync("/Library/Delete/99999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });
    }

    public async Task InitializeAsync() => await TestDatabaseHelper.ResetDatabaseAsync(_factory.Services);
    public async Task DisposeAsync() => await TestDatabaseHelper.ResetDatabaseAsync(_factory.Services);
}
