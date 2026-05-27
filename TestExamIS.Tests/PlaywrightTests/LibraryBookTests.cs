using Microsoft.Playwright;
using TestExamIS.Tests.Utils;

namespace TestExamIS.Tests.PlaywrightTests;

// Playwright tests require the app running on http://localhost:5210
// Start first: dotnet run --project BookBorrowingSystem
[Collection("Playwright Suite")]
public class LibraryBookTests : LoggedTestBase
{
    private const string BaseUrl = "http://localhost:5210";
    private readonly PlaywrightFixture _playwright;

    public LibraryBookTests(PlaywrightFixture playwrightFixture, AppFixture _) : base(playwrightFixture)
    {
        _playwright = playwrightFixture;
    }

    private async Task<IPage> NewPageAsync() => await _playwright.Browser.NewPageAsync();

    // ── Req 6a: Library/Details has id="add-book" link ────────

    [LoggedFact(Category = "PlaywrightUI", Points = 5)]
    public async Task LibraryDetails_HasAddBookLinkWithCorrectId()
    {
        await RunTestAsync(async () =>
        {
            var page = await NewPageAsync();
            try
            {
                await page.GotoAsync($"{BaseUrl}/Library");
                var firstDetailsLink = page.Locator("a[href*='/Library/Details/']").First;
                var href = await firstDetailsLink.GetAttributeAsync("href");
                Assert.NotNull(href);

                await page.GotoAsync($"{BaseUrl}{href}");

                var addBookLink = page.Locator("#add-book");
                await Assertions.Expect(addBookLink).ToBeVisibleAsync();

                var linkText = await addBookLink.InnerTextAsync();
                Assert.Contains("Додади книга", linkText);
            }
            finally { await page.CloseAsync(); }
        });
    }

    // ── Req 6b: Library/Details books table has id="books-table"

    [LoggedFact(Category = "PlaywrightUI", Points = 5)]
    public async Task LibraryDetails_BooksTableHasCorrectId()
    {
        await RunTestAsync(async () =>
        {
            var page = await NewPageAsync();
            try
            {
                await page.GotoAsync($"{BaseUrl}/Library");
                var href = await page.Locator("a[href*='/Library/Details/']").First.GetAttributeAsync("href");
                await page.GotoAsync($"{BaseUrl}{href}");

                await Assertions.Expect(page.Locator("#books-table")).ToBeVisibleAsync();
            }
            finally { await page.CloseAsync(); }
        });
    }

    // ── Req 6b: Details links have class="details-btn" ────────

    [LoggedFact(Category = "PlaywrightUI", Points = 2)]
    public async Task LibraryDetails_DetailsBtnHasCorrectClass()
    {
        await RunTestAsync(async () =>
        {
            var page = await NewPageAsync();
            try
            {
                await page.GotoAsync($"{BaseUrl}/Library");
                var href = await page.Locator("a[href*='/Library/Details/']").First.GetAttributeAsync("href");
                await page.GotoAsync($"{BaseUrl}{href}");

                var count = await page.Locator(".details-btn").CountAsync();
                Assert.True(count > 0, "Expected at least one element with class 'details-btn'.");
            }
            finally { await page.CloseAsync(); }
        });
    }

    // ── Req 6b: Borrow links have class="borrow-btn" ──────────

    [LoggedFact(Category = "PlaywrightUI", Points = 2)]
    public async Task LibraryDetails_BorrowBtnHasCorrectClass()
    {
        await RunTestAsync(async () =>
        {
            var page = await NewPageAsync();
            try
            {
                await page.GotoAsync($"{BaseUrl}/Library");
                var href = await page.Locator("a[href*='/Library/Details/']").First.GetAttributeAsync("href");
                await page.GotoAsync($"{BaseUrl}{href}");

                var count = await page.Locator(".borrow-btn").CountAsync();
                Assert.True(count > 0, "Expected at least one element with class 'borrow-btn'.");
            }
            finally { await page.CloseAsync(); }
        });
    }

    // ── Req 7c: Member/Details return button class ────────────

    [LoggedFact(Category = "PlaywrightUI", Points = 5)]
    public async Task MemberDetails_ReturnBtnHasCorrectClass()
    {
        await RunTestAsync(async () =>
        {
            var page = await NewPageAsync();
            try
            {
                await page.GotoAsync($"{BaseUrl}/Member");
                var href = await page.Locator("a[href*='/Member/Details/']").First.GetAttributeAsync("href");
                await page.GotoAsync($"{BaseUrl}{href}");

                var count = await page.Locator(".return-btn").CountAsync();
                Assert.True(count > 0, "Expected at least one element with class 'return-btn'.");
            }
            finally { await page.CloseAsync(); }
        });
    }

    // ── Req 5c: Member/Tabulator loads data ───────────────────

    [LoggedFact(Category = "PlaywrightUI", Points = 3)]
    public async Task MemberTabulator_LoadsDataInTable()
    {
        await RunTestAsync(async () =>
        {
            var page = await NewPageAsync();
            try
            {
                await page.GotoAsync($"{BaseUrl}/Member/Tabulator");

                await Assertions.Expect(page.Locator("#tabulator-table")).ToBeVisibleAsync();
                await page.WaitForSelectorAsync(".tabulator-row", new() { Timeout = 5000 });

                var rowCount = await page.Locator(".tabulator-row").CountAsync();
                Assert.True(rowCount > 0, "Tabulator table should have at least one row.");
            }
            finally { await page.CloseAsync(); }
        });
    }
}

[CollectionDefinition("Playwright Suite", DisableParallelization = true)]
public class PlaywrightSuiteCollection : ICollectionFixture<PlaywrightFixture>, ICollectionFixture<AppFixture> { }
