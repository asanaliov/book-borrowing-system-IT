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
public class BookControllerTests : LoggedTestBase, IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public BookControllerTests(WebApplicationFactory<Program> factory, GlobalTestFixture fixture) : base(fixture)
    {
        _factory = factory.WithTestDatabase();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false,
        });
        _client.Timeout = TimeSpan.FromSeconds(10);
    }

    // ── Req 1a: CoverImageUrl rendered as <img> ─────────────

    [LoggedFact(Category = "BookController", Points = 5)]
    public async Task Index_ShowsCoverImageAsImg()
    {
        await RunTestAsync(async () =>
        {
            var response = await _client.GetAsync("/Book");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            // Book images must appear as <img> tags, not plain URL text
            Assert.Contains("<img", content, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("example.com/covers", content);
        });
    }

    // ── Req 7e: Book/Details shows active borrowings ─────────

    [LoggedFact(Category = "BookController", Points = 5)]
    public async Task Details_ShowsCurrentBorrowingsWhereReturnDateIsNull()
    {
        await RunTestAsync(async () =>
        {
            // book1 (seeded) has 2 active borrowings (ReturnDate == null)
            var book = await GetBookWithActiveborrowingsAsync();

            var response = await _client.GetAsync($"/Book/Details/{book.Id}");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            // Active borrowers' names should appear
            Assert.Contains("Александар", content);
            Assert.Contains("Марија", content);
        });
    }

    [LoggedFact(Category = "BookController", Points = 1)]
    public async Task Details_InvalidId_ReturnsNotFound()
    {
        await RunTestAsync(async () =>
        {
            var response = await _client.GetAsync("/Book/Details/99999");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });
    }

    // ── Req 7a: Book/Borrow GET ────────────────────────────────

    [LoggedFact(Category = "BookController", Points = 5)]
    public async Task Borrow_GET_ShowsBookTitle()
    {
        await RunTestAsync(async () =>
        {
            var book = await TestDatabaseHelper.GetFirst<Book>(_factory.Services);

            var response = await _client.GetAsync($"/Book/Borrow/{book.Id}");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            Assert.Contains("id=\"book-title\"", content);
            Assert.Contains(book.Title, content);
        });
    }

    [LoggedFact(Category = "BookController", Points = 5)]
    public async Task Borrow_GET_ShowsLibraryName()
    {
        await RunTestAsync(async () =>
        {
            var book = await TestDatabaseHelper.GetFirst<Book>(_factory.Services);

            var response = await _client.GetAsync($"/Book/Borrow/{book.Id}");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            Assert.Contains("id=\"library-name\"", content);
        });
    }

    [LoggedFact(Category = "BookController", Points = 5)]
    public async Task Borrow_GET_ShowsMemberDropdown()
    {
        await RunTestAsync(async () =>
        {
            var book = await TestDatabaseHelper.GetFirst<Book>(_factory.Services);

            var response = await _client.GetAsync($"/Book/Borrow/{book.Id}");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            Assert.Contains("<select", content, StringComparison.OrdinalIgnoreCase);
        });
    }

    // ── Req 7b: Book/Borrow POST ───────────────────────────────

    [LoggedFact(Category = "BookController", Points = 5)]
    public async Task Borrow_POST_SavesBorrowingAndRedirectsToBookDetails()
    {
        await RunTestAsync(async () =>
        {
            var book = await TestDatabaseHelper.GetFirst<Book>(_factory.Services);
            var member = await TestDatabaseHelper.GetFirst<Member>(_factory.Services);
            var initialCount = await TestDatabaseHelper.GetCount<BookBorrowing>(_factory.Services);

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("BookId", book.Id.ToString()),
                new KeyValuePair<string, string>("MemberId", member.Id.ToString()),
            });

            var response = await _client.PostAsync("/Book/Borrow", formContent);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains($"/Book/Details/{book.Id}", response.Headers.Location?.ToString());

            var newCount = await TestDatabaseHelper.GetCount<BookBorrowing>(_factory.Services);
            Assert.Equal(initialCount + 1, newCount);

            // ReturnDate must be null for the new borrowing
            var borrowing = TestDatabaseHelper.GetById<BookBorrowing>(
                _factory.Services,
                b => b.BookId == book.Id && b.MemberId == member.Id && b.ReturnDate == null);
            Assert.NotNull(borrowing);
        });
    }

    // ── Req 6a: After AddBook, redirect to Library/Details ────

    [LoggedFact(Category = "BookController", Points = 5)]
    public async Task AddBook_AfterCreate_RedirectsToLibraryDetails()
    {
        await RunTestAsync(async () =>
        {
            var library = await TestDatabaseHelper.GetFirst<Library>(_factory.Services);
            var initialCount = await TestDatabaseHelper.GetCount<Book>(_factory.Services);

            var getResponse = await _client.GetAsync("/Book/Create");
            var antiForgeryToken = await getResponse.GetAntiForgeryTokenAsync();

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Title", "Нова книга"),
                new KeyValuePair<string, string>("Author", "Тест Автор"),
                new KeyValuePair<string, string>("Genre", "Роман"),
                new KeyValuePair<string, string>("CoverImageUrl", "https://example.com/test.jpg"),
                new KeyValuePair<string, string>("TotalCopies", "3"),
                new KeyValuePair<string, string>("LibraryId", library.Id.ToString()),
                new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken)
            });

            var response = await _client.PostAsync("/Book/Create", formContent);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains($"/Library/Details/{library.Id}", response.Headers.Location?.ToString());

            var newCount = await TestDatabaseHelper.GetCount<Book>(_factory.Services);
            Assert.Equal(initialCount + 1, newCount);
        });
    }

    // ── CRUD: Create ───────────────────────────────────────────

    [LoggedFact(Category = "BookController", Points = 1)]
    public async Task Create_ValidBook_SavesAndRedirects()
    {
        await RunTestAsync(async () =>
        {
            var library = await TestDatabaseHelper.GetFirst<Library>(_factory.Services);
            var initialCount = await TestDatabaseHelper.GetCount<Book>(_factory.Services);

            var getResponse = await _client.GetAsync("/Book/Create");
            var antiForgeryToken = await getResponse.GetAntiForgeryTokenAsync();

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Title", "Втора нова книга"),
                new KeyValuePair<string, string>("Author", "Автор 2"),
                new KeyValuePair<string, string>("Genre", "Поезија"),
                new KeyValuePair<string, string>("CoverImageUrl", "https://example.com/book2.jpg"),
                new KeyValuePair<string, string>("TotalCopies", "2"),
                new KeyValuePair<string, string>("LibraryId", library.Id.ToString()),
                new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken)
            });

            var response = await _client.PostAsync("/Book/Create", formContent);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            var newCount = await TestDatabaseHelper.GetCount<Book>(_factory.Services);
            Assert.Equal(initialCount + 1, newCount);
        });
    }

    // ── CRUD: Delete ───────────────────────────────────────────

    [LoggedFact(Category = "BookController", Points = 1)]
    public async Task Delete_ValidBook_RemovesAndRedirects()
    {
        await RunTestAsync(async () =>
        {
            var initialCount = await TestDatabaseHelper.GetCount<Book>(_factory.Services);
            var book = await TestDatabaseHelper.GetFirst<Book>(_factory.Services);

            var getResponse = await _client.GetAsync($"/Book/Delete/{book.Id}");
            var antiForgeryToken = await getResponse.GetAntiForgeryTokenAsync();

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken)
            });

            var response = await _client.PostAsync($"/Book/Delete/{book.Id}", formContent);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            var deleted = TestDatabaseHelper.GetById<Book>(_factory.Services, x => x.Id == book.Id);
            Assert.Null(deleted);

            var newCount = await TestDatabaseHelper.GetCount<Book>(_factory.Services);
            Assert.Equal(initialCount - 1, newCount);
        });
    }

    // ── Helpers ────────────────────────────────────────────────

    private async Task<Book> GetBookWithActiveborrowingsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // book1 is seeded with 2 active (ReturnDate == null) borrowings
        return await db.Books
            .Where(b => b.Borrowings.Any(br => br.ReturnDate == null))
            .FirstAsync();
    }

    public async Task InitializeAsync() => await TestDatabaseHelper.ResetDatabaseAsync(_factory.Services);
    public async Task DisposeAsync() => await TestDatabaseHelper.ResetDatabaseAsync(_factory.Services);
}
