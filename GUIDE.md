# Solution Guide — Book Borrowing System

A step-by-step learning guide for solving the Internet Technologies midterm task. Each step explains **what** to do, **why** it works that way, and **what to watch out for**.

> Read [Task.md](./Task.md) first for the full requirements.

---

## Table of Contents

1. [Mental model — how ASP.NET Core MVC fits together](#step-0-mental-model)
2. [Foundation — models, relations, DbContext, migrations](#step-1-foundation)
3. [Model validation (Task 3)](#step-2-model-validation-task-3)
4. [CRUD controllers + views (Task 1)](#step-3-crud-controllers--views-task-1)
5. [Library index customizations (Task 1a/b/c)](#step-4-library-index-customizations)
6. [Filtering libraries (Task 4)](#step-5-filtering-libraries-task-4)
7. [Library Details + AddBook (Task 6)](#step-6-library-details--addbook-task-6)
8. [Borrowing flow (Task 7)](#step-7-borrowing-flow-task-7)
9. [Tabulator.js (Task 5)](#step-8-tabulatorjs-task-5)
10. [Authorization & seeding (Task 8)](#step-9-authorization--seeding-task-8)
11. [Common pitfalls & debugging tips](#common-pitfalls--debugging-tips)
12. [Final checklist](#final-checklist)

---

## Step 0: Mental model

Before writing code, understand the moving pieces.

### Request lifecycle

```
Browser → URL → Routing → Controller action → (DbContext → SQLite) → View (Razor) → HTML → Browser
```

- **Routing** uses the pattern `{controller=Home}/{action=Index}/{id?}` from `Program.cs`. So `/Library/Details/5` calls `LibraryController.Details(5)`.
- **Controller** holds the logic — fetch data, validate, decide which view to render.
- **DbContext** (`ApplicationDbContext`) is your gateway to the DB via Entity Framework Core. Each `DbSet<T>` represents a table.
- **View** is a `.cshtml` file in `Views/{ControllerName}/{ActionName}.cshtml` — Razor syntax mixes HTML with C#.

### Key conventions

| Convention | Where |
|---|---|
| Controller file: `XController.cs` in `Controllers/` | required by naming convention |
| Views in `Views/X/Action.cshtml` | required for default view resolution |
| Default action: `Index` | when URL has no action |
| `[HttpGet]` (default) vs `[HttpPost]` | form submissions are POST |
| ModelState validation: `if (ModelState.IsValid)` | runs `[Required]`/`[RegularExpression]` checks |

### Entity Framework Core in 60 seconds

EF Core is an **ORM** — it maps C# classes to SQL tables. Each model class = a table. Each `DbSet<T>` = a queryable table reference.

- `_context.Libraries.ToList()` → `SELECT * FROM Libraries`
- `_context.Libraries.Find(5)` → `SELECT * FROM Libraries WHERE Id = 5`
- `_context.Libraries.Add(lib); _context.SaveChanges()` → `INSERT INTO Libraries ...`
- `_context.Books.Include(b => b.Library)` → adds `JOIN Libraries`. **Without `Include`, navigation properties are `null`!**
- Migrations = versioned SQL scripts EF generates from your model classes.

---

## Step 1: Foundation

The starter has models without relations. The DB schema (in the existing `Initial` migration) already has FK columns — we just need to make the C# code match.

### 1.1 Models — add navigation properties

**Rule of thumb:** every relation needs **both** a foreign-key property (`int LibraryId`) **and** a navigation property (`Library Library`). The collection side gets `ICollection<T>`.

#### `Models/Book.cs`

```csharp
namespace LibraryApplication.Models;

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Author { get; set; }
    public string Genre { get; set; }
    public string CoverImageUrl { get; set; }
    public int TotalCopies { get; set; }

    public int LibraryId { get; set; }
    public Library Library { get; set; }

    public ICollection<BookBorrowing> Borrowings { get; set; } = new List<BookBorrowing>();
}
```

**Why?**
- `LibraryId` is what's actually stored in the DB row.
- `Library` is what you traverse in code (`book.Library.Name`) or in views (`@Model.Library.Name`).
- `Borrowings` is the inverse side — "all borrowings of this book". Initialized to avoid `NullReferenceException`.

#### `Models/Library.cs`

```csharp
public class Library
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Address { get; set; }
    public string City { get; set; }
    public string Country { get; set; }
    public double Rating { get; set; }

    public ICollection<Book> Books { get; set; } = new List<Book>();
}
```

#### `Models/BookBorrowing.cs`

```csharp
namespace LibraryApplication.Models;

public class BookBorrowing
{
    public int Id { get; set; }

    public int BookId { get; set; }
    public Book Book { get; set; }

    public int MemberId { get; set; }
    public Member Member { get; set; }

    public DateTime BorrowDate { get; set; }
    public DateTime? ReturnDate { get; set; }   // nullable: null = not returned yet
}
```

**Learning point — nullable reference types:** the `?` in `DateTime?` means "this can be null". For value types it's `Nullable<DateTime>` under the hood. For reference types (strings), `?` is just a compiler hint that doesn't change runtime behavior.

### 1.2 DbContext — register DbSets

`Data/ApplicationDbContext.cs`:

```csharp
using LibraryApplication.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BookBorrowingSystem.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Library> Libraries { get; set; }
    public DbSet<Book> Books { get; set; }
    public DbSet<Member> Members { get; set; }
    public DbSet<BookBorrowing> BookBorrowings { get; set; }
}
```

**Why?** Without `DbSet<T>`, EF Core doesn't know the entity exists. You couldn't write `_context.Libraries.ToList()` — it wouldn't compile.

**Learning point — primary constructors:** `class ApplicationDbContext(DbContextOptions<...> options) : IdentityDbContext(options)` is C# 12 primary constructor syntax. The `options` parameter is automatically passed to the base class. Older code style would have a full constructor block.

### 1.3 Migration

Open Package Manager Console (Tools → NuGet Package Manager → Package Manager Console):

```powershell
Add-Migration AddNavigationProperties
Update-Database
```

Since the `Initial` migration already created the FK columns, this new migration will likely be empty (good — confirms your code matches the DB).

**Learning point — what a migration is:** EF compares your current code against `ApplicationDbContextModelSnapshot.cs` (a snapshot of "what the model looked like last migration"), and generates a script for the diff. `Up` migrates forward, `Down` rolls back.

### ✅ Checkpoint

- All 4 models have relations
- `ApplicationDbContext` has 4 `DbSet`s
- Project builds with no errors
- `Update-Database` succeeded

---

## Step 2: Model validation (Task 3)

Already covered if you used the `Member` code above, but here's the deep dive.

```csharp
using System.ComponentModel.DataAnnotations;

namespace LibraryApplication.Models;

public class Member
{
    public int Id { get; set; }

    [Required]
    public string FirstName { get; set; }

    [Required]
    public string LastName { get; set; }

    [Display(Name = "Е-адреса")]
    public string Email { get; set; }

    [RegularExpression(@"^\d{9}$",
        ErrorMessage = "Телефонскиот број мора да биде составен од точно 9 цифри.")]
    public string PhoneNumber { get; set; }

    public DateTime MembershipDate { get; set; }

    public ICollection<BookBorrowing> Borrowings { get; set; } = new List<BookBorrowing>();
}
```

**Deep dive — how validation actually runs:**
1. ASP.NET model binding reads form fields and populates the `Member` object.
2. Each attribute's `Validate()` is called against the value.
3. Errors land in `ModelState`.
4. In the controller, `if (ModelState.IsValid)` lets you check.
5. In the view, `@Html.ValidationMessageFor(m => m.PhoneNumber)` renders the error inline.

**Regex breakdown — `^\d{9}$`:**
- `^` — start of string
- `\d` — any digit `0-9`
- `{9}` — exactly 9 times
- `$` — end of string

Without `^` and `$`, `"12345678901234"` would also match (it *contains* 9 consecutive digits).

**Display attribute** — affects how property names are rendered by `@Html.LabelFor(m => m.Email)` and tag helpers like `<label asp-for="Email">`. The label will read "Е-адреса" instead of "Email".

---

## Step 3: CRUD controllers + views (Task 1)

### 3.1 Scaffold the controllers

In Visual Studio:
- Right-click `Controllers/` → Add → Controller
- Choose **MVC Controller with views, using Entity Framework**
- Model: `Library`, DbContext: `ApplicationDbContext`
- Name: `LibraryController`
- Repeat for `Book` and `Member`.

This generates the controller + 5 views (`Index`, `Create`, `Edit`, `Details`, `Delete`).

**Learning point — what scaffolding actually creates:**
- **Index** (GET): `return View(await _context.Libraries.ToListAsync());`
- **Create** (GET): empty form. (POST): `_context.Add(lib); SaveChanges(); RedirectToAction("Index");`
- **Edit** (GET): pre-filled form. (POST): `_context.Update(lib); SaveChanges();`
- **Details** (GET): single record by id.
- **Delete** (GET): confirmation page. (POST + `DeleteConfirmed`): actual delete.

### 3.2 Quick orientation — read what was scaffolded

Open `Controllers/LibraryController.cs` and identify:
- The constructor takes `ApplicationDbContext` — that's **dependency injection** in action. Registered in `Program.cs` via `AddDbContext<ApplicationDbContext>(...)`.
- `await _context.Libraries.ToListAsync()` — async DB query.
- `[Bind("Id,Name,Address,...")]` on POST actions — explicit allowlist of fields the model binder will accept. Protects against over-posting attacks.

### 3.3 Eager-load related data

The scaffolded `BookController.Index()` will be `return View(await _context.Books.ToListAsync());`. But you need book.Library.Name — change it to:

```csharp
public async Task<IActionResult> Index()
{
    var books = _context.Books.Include(b => b.Library);
    return View(await books.ToListAsync());
}
```

Do the same in `Details`, `Edit`, `Delete` where you need related data.

> **Why?** Without `Include`, `book.Library` is `null` and `book.Library.Name` throws a `NullReferenceException` when the view tries to render.

### 3.4 The Book form needs a Library dropdown

Edit `BookController.Create` (GET):

```csharp
public IActionResult Create()
{
    ViewData["LibraryId"] = new SelectList(_context.Libraries, "Id", "Name");
    return View();
}
```

This builds a dropdown of libraries — value is `Id`, displayed text is `Name`. Repeat in `Edit`.

In `Views/Book/Create.cshtml`, the scaffolded view should have:

```html
<select asp-for="LibraryId" class="form-control" asp-items="ViewBag.LibraryId"></select>
```

If it's missing or showing as a text input, add it manually.

### ✅ Checkpoint

- Three controllers exist (`LibraryController`, `BookController`, `MemberController`)
- You can navigate to `/Library`, `/Book`, `/Member` and see lists
- You can create, edit, delete a Library, Book, Member
- Creating a Book lets you pick a Library from a dropdown

---

## Step 4: Library index customizations

### 4.1 Book cover as image (Task 1a)

In `Views/Book/Index.cshtml`, find the cell rendering `CoverImageUrl` (it'll be a `@Html.DisplayFor` line). Replace with:

```html
<td>
    <img src="@item.CoverImageUrl" style="max-width: 150px;" alt="@item.Title" />
</td>
```

Same in `Views/Book/Details.cshtml`:

```html
<dt class="col-sm-2">Cover</dt>
<dd class="col-sm-10">
    <img src="@Model.CoverImageUrl" style="max-width: 150px;" alt="@Model.Title" />
</dd>
```

### 4.2 Library name as link to Details (Task 1b)

In `Views/Library/Index.cshtml`, find the row rendering `item.Name`. Replace with:

```html
<td>
    <a asp-action="Details" asp-route-id="@item.Id">@item.Name</a>
</td>
```

**Learning point — tag helpers:** `asp-action`, `asp-route-id`, `asp-controller` are ASP.NET Core tag helpers that generate URLs at render time. `asp-route-id="@item.Id"` produces `?id=5` or `/5` depending on route config.

### 4.3 Book count column (Task 1c)

You have two options. The cleaner one:

**In the controller** (`LibraryController.Index`), use a view model or pass via `ViewBag`. Easiest: include books in the query:

```csharp
public async Task<IActionResult> Index()
{
    var libraries = await _context.Libraries
        .Include(l => l.Books)
        .ToListAsync();
    return View(libraries);
}
```

Then in `Views/Library/Index.cshtml`, add a column:

```html
<th>Број на книги</th>
...
<td>@item.Books.Count</td>
```

**Alternative** (more efficient — doesn't load the books themselves):

```csharp
var libraries = await _context.Libraries
    .Select(l => new { Library = l, BookCount = l.Books.Count })
    .ToListAsync();
```

But that requires a view model, so for a midterm just use `.Include`.

---

## Step 5: Filtering libraries (Task 4)

### 5.1 Controller

```csharp
public async Task<IActionResult> Index(string? name, string? city)
{
    var query = _context.Libraries.Include(l => l.Books).AsQueryable();

    if (!string.IsNullOrWhiteSpace(name))
        query = query.Where(l => l.Name.Contains(name));

    if (!string.IsNullOrWhiteSpace(city))
        query = query.Where(l => l.City.Contains(city));

    ViewBag.Name = name;
    ViewBag.City = city;

    return View(await query.ToListAsync());
}
```

**Learning point — `IQueryable<T>` vs `IEnumerable<T>`:**
- `IQueryable` builds an expression tree that gets translated to SQL. `.Where(...)` adds a `WHERE` clause to the eventual query.
- `IEnumerable` is in-memory — calling `.Where` on `ToList()`-ed data filters in C#, after pulling everything.
- Filter on `IQueryable` whenever possible — let the DB do the work.

### 5.2 View — filter form

At the top of `Views/Library/Index.cshtml`:

```html
<form asp-action="Index" method="get" class="mb-3">
    <div class="row g-2">
        <div class="col-auto">
            <input type="text" name="Name" value="@ViewBag.Name" class="form-control" placeholder="Name" />
        </div>
        <div class="col-auto">
            <input type="text" name="City" value="@ViewBag.City" class="form-control" placeholder="City" />
        </div>
        <div class="col-auto">
            <button type="submit" class="btn btn-primary">Филтрирај</button>
        </div>
    </div>
</form>
```

**Why `name="Name"` and `name="City"` (capitalized)?** The task explicitly requires this. ASP.NET model binding is case-insensitive by default, but the test harness likely checks the literal attribute. Match the spec exactly.

**Why `method="get"`?** Filtering is a "view" action, not a mutation. GET keeps filters in the URL — shareable, bookmarkable, browser-back-friendly.

---

## Step 6: Library Details + AddBook (Task 6)

### 6.1 Load books in Library.Details

```csharp
public async Task<IActionResult> Details(int? id)
{
    if (id == null) return NotFound();

    var library = await _context.Libraries
        .Include(l => l.Books)
        .FirstOrDefaultAsync(l => l.Id == id);

    if (library == null) return NotFound();

    return View(library);
}
```

### 6.2 `AddBook` actions in `LibraryController`

```csharp
[HttpGet]
public async Task<IActionResult> AddBook(int id)
{
    var library = await _context.Libraries.FindAsync(id);
    if (library == null) return NotFound();

    ViewBag.LibraryId = id;
    ViewBag.LibraryName = library.Name;
    return View();
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> AddBook(int id, [Bind("Title,Author,Genre,CoverImageUrl,TotalCopies")] Book book)
{
    var library = await _context.Libraries.FindAsync(id);
    if (library == null) return NotFound();

    book.LibraryId = id;

    if (ModelState.IsValid)
    {
        _context.Add(book);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    ViewBag.LibraryId = id;
    ViewBag.LibraryName = library.Name;
    return View(book);
}
```

**Why redirect to `Details`?** The task requires it: "After successful creation, always redirect to Details of the library where the book was created." Use `nameof(Details)` instead of the string `"Details"` — refactor-safe.

**Why `[ValidateAntiForgeryToken]`?** Pairs with the auto-generated `@Html.AntiForgeryToken()` in the form. Prevents CSRF attacks.

### 6.3 `Views/Library/AddBook.cshtml`

```html
@model LibraryApplication.Models.Book
@{
    ViewData["Title"] = "Add Book";
}

<h1>Додади книга</h1>
<h4>Библиотека: @ViewBag.LibraryName</h4>

<form asp-action="AddBook" asp-route-id="@ViewBag.LibraryId" method="post">
    <div asp-validation-summary="ModelOnly" class="text-danger"></div>

    <div class="form-group">
        <label asp-for="Title"></label>
        <input asp-for="Title" class="form-control" />
        <span asp-validation-for="Title" class="text-danger"></span>
    </div>
    <!-- repeat for Author, Genre, CoverImageUrl, TotalCopies -->

    <button type="submit" class="btn btn-primary">Зачувај</button>
</form>

@section Scripts {
    @{await Html.RenderPartialAsync("_ValidationScriptsPartial");}
}
```

### 6.4 `Views/Library/Details.cshtml` — Add Book link + books table

Inside Details, after the library info:

```html
<a id="add-book" asp-action="AddBook" asp-route-id="@Model.Id" class="btn btn-primary">Додади книга</a>

<h3 class="mt-4">Книги</h3>
<table id="books-table" class="table">
    <thead>
        <tr>
            <th>Title</th>
            <th>Author</th>
            <th>Genre</th>
            <th>Cover</th>
            <th></th>
        </tr>
    </thead>
    <tbody>
    @foreach (var book in Model.Books)
    {
        <tr>
            <td>@book.Title</td>
            <td>@book.Author</td>
            <td>@book.Genre</td>
            <td><img src="@book.CoverImageUrl" width="80" /></td>
            <td>
                <a class="details-btn" asp-controller="Book" asp-action="Details" asp-route-id="@book.Id">Details</a>
                <a class="borrow-btn" asp-controller="Book" asp-action="Borrow" asp-route-id="@book.Id">Позајми</a>
            </td>
        </tr>
    }
    </tbody>
</table>
```

> **The grader checks specific `id` and `class` attributes.** `id="add-book"`, `id="books-table"`, `class="details-btn"`, `class="borrow-btn"` — match exactly.

---

## Step 7: Borrowing flow (Task 7)

This is the biggest section. Take it slow.

### 7.1 `BookController.Borrow` (GET)

```csharp
[HttpGet]
public async Task<IActionResult> Borrow(int id)
{
    var book = await _context.Books
        .Include(b => b.Library)
        .FirstOrDefaultAsync(b => b.Id == id);

    if (book == null) return NotFound();

    ViewBag.Members = new SelectList(_context.Members, "Id", "FirstName");
    return View(book);
}
```

### 7.2 `Views/Book/Borrow.cshtml`

```html
@model LibraryApplication.Models.Book

<h1 id="book-title">@Model.Title</h1>
<h4 id="library-name">@Model.Library.Name</h4>

<form asp-action="Borrow" method="post">
    <input type="hidden" name="BookId" value="@Model.Id" />

    <div class="form-group">
        <label>Член</label>
        <select name="MemberId" asp-items="ViewBag.Members" class="form-control"></select>
    </div>

    <button type="submit" class="btn btn-primary">Позајми</button>
</form>
```

### 7.3 `BookController.Borrow` (POST)

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Borrow(int BookId, int MemberId)
{
    var borrowing = new BookBorrowing
    {
        BookId = BookId,
        MemberId = MemberId,
        BorrowDate = DateTime.Now,
        ReturnDate = null
    };

    _context.BookBorrowings.Add(borrowing);
    await _context.SaveChangesAsync();

    return RedirectToAction(nameof(Details), new { id = BookId });
}
```

> **Note:** Use the exact parameter names `BookId` and `MemberId` so model binding picks them up from the form. The casing doesn't matter for binding, but matching the spec is safer.

### 7.4 `Member/Details` — borrowings table + Return button

`MemberController.Details`:

```csharp
public async Task<IActionResult> Details(int? id)
{
    if (id == null) return NotFound();

    var member = await _context.Members
        .Include(m => m.Borrowings)
            .ThenInclude(b => b.Book)
        .FirstOrDefaultAsync(m => m.Id == id);

    if (member == null) return NotFound();

    return View(member);
}
```

**Learning point — `ThenInclude`:** chains a second-level navigation. We're loading the member, then the borrowings, then each borrowing's book.

In `Views/Member/Details.cshtml`, add at the bottom:

```html
<h3>Позајмени книги</h3>
<table class="table">
    <thead>
        <tr>
            <th>Title</th>
            <th>BorrowDate</th>
            <th>ReturnDate</th>
            <th></th>
        </tr>
    </thead>
    <tbody>
    @foreach (var b in Model.Borrowings)
    {
        <tr>
            <td>@b.Book.Title</td>
            <td>@b.BorrowDate.ToString("yyyy-MM-dd")</td>
            <td>@(b.ReturnDate?.ToString("yyyy-MM-dd") ?? "Не е вратена")</td>
            <td>
                @if (b.ReturnDate == null)
                {
                    <form asp-controller="Members" asp-action="ReturnBook" asp-route-borrowingId="@b.Id" method="post" style="display:inline">
                        @Html.AntiForgeryToken()
                        <button type="submit" class="return-btn btn btn-warning">Врати</button>
                    </form>
                }
            </td>
        </tr>
    }
    </tbody>
</table>
```

> ⚠️ The task says `Members/ReturnBook` (plural). Either name your controller `MembersController` *and* route the rest of the actions to `/Members`, or add `[Route("Members/[action]")]` to just this action. Read the spec carefully — if other Member URLs use `/Member`, you might need a custom route here.

### 7.5 `MemberController.ReturnBook`

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ReturnBook(int borrowingId)
{
    var borrowing = await _context.BookBorrowings.FindAsync(borrowingId);
    if (borrowing == null) return NotFound();

    borrowing.ReturnDate = DateTime.Now;
    await _context.SaveChangesAsync();

    return RedirectToAction(nameof(Details), new { id = borrowing.MemberId });
}
```

### 7.6 `Book/Details` — current borrowings table

In `BookController.Details`, include borrowings:

```csharp
var book = await _context.Books
    .Include(b => b.Library)
    .Include(b => b.Borrowings.Where(br => br.ReturnDate == null))
        .ThenInclude(br => br.Member)
    .FirstOrDefaultAsync(b => b.Id == id);
```

**Learning point — filtered Include (EF Core 5+):** you can filter what gets loaded in the included collection. Here we only load active borrowings.

In `Views/Book/Details.cshtml`:

```html
<h3>Тековни позајмувања</h3>
<table class="table">
    <thead><tr><th>Member</th><th>BorrowDate</th></tr></thead>
    <tbody>
    @foreach (var b in Model.Borrowings)
    {
        <tr>
            <td>@b.Member.FirstName @b.Member.LastName</td>
            <td>@b.BorrowDate.ToString("yyyy-MM-dd HH:mm")</td>
        </tr>
    }
    </tbody>
</table>
```

---

## Step 8: Tabulator.js (Task 5)

Tabulator's CSS and JS are already in `_Layout.cshtml`.

### 8.1 API endpoint in `MemberController`

```csharp
[HttpGet]
public async Task<IActionResult> Api()
{
    var members = await _context.Members
        .Select(m => new
        {
            m.Id,
            m.FirstName,
            m.LastName,
            m.Email,
            m.PhoneNumber,
            MembershipDate = m.MembershipDate.ToString("yyyy-MM-dd")
        })
        .ToListAsync();
    return Json(members);
}
```

**Why a projected anonymous object?** Avoid leaking everything (don't return EF entities with nav-property cycles directly — they cause serialization loops). Also lets you format the date.

### 8.2 `Tabulator` action

```csharp
public IActionResult Tabulator() => View();
```

### 8.3 `Views/Member/Tabulator.cshtml`

```html
@{
    ViewData["Title"] = "Members (Tabulator)";
}

<h1>Членови</h1>
<div id="members-table"></div>

@section Scripts {
<script>
    new Tabulator("#members-table", {
        ajaxURL: "/Member/Api",
        layout: "fitColumns",
        columns: [
            { title: "FirstName", field: "FirstName" },
            { title: "LastName", field: "LastName" },
            { title: "Е-адреса", field: "Email" },
            { title: "PhoneNumber", field: "PhoneNumber" },
            { title: "MembershipDate", field: "MembershipDate" },
            {
                title: "",
                formatter: function(cell) {
                    var id = cell.getRow().getData().Id;
                    return '<a href="/Member/Details/' + id + '">Details</a>';
                }
            }
        ]
    });
</script>
}
```

**Learning point — `formatter`:** Tabulator lets you customize cell rendering via a JS function. `cell.getRow().getData()` gives the full row data.

Add a nav link to `/Member/Tabulator` in your menu if you want it discoverable.

---

## Step 9: Authorization & seeding (Task 8)

### 9.1 Enable roles in `Program.cs`

The starter has `AddDefaultIdentity<IdentityUser>(...)` — that doesn't include role support. Change to:

```csharp
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
```

### 9.2 Seed roles + users

Create `Data/SeedData.cs`:

```csharp
using Microsoft.AspNetCore.Identity;

namespace BookBorrowingSystem.Data;

public static class SeedData
{
    public static async Task SeedRolesAndUsersAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        string[] roles = { "User", "Librarian", "Admin" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var users = new[]
        {
            ("user@test.com", "User"),
            ("librarian@test.com", "Librarian"),
            ("admin@test.com", "Admin")
        };

        foreach (var (email, role) in users)
        {
            if (await userManager.FindByEmailAsync(email) == null)
            {
                var user = new IdentityUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(user, "Password1@");
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }
}
```

In `Program.cs`, **after** `var app = builder.Build();`:

```csharp
using (var scope = app.Services.CreateScope())
{
    await SeedData.SeedRolesAndUsersAsync(scope.ServiceProvider);
}
```

**Learning point — service scopes:** `RoleManager`/`UserManager` are scoped services. The DI container will complain if you try to resolve them from the root provider. `CreateScope()` gives a temporary scope you can resolve from.

### 9.3 `[Authorize]` attributes

In `LibraryController` and `BookController`:

```csharp
[Authorize(Roles = "Admin,Librarian")]
public class LibraryController : Controller
{
    [AllowAnonymous]
    public async Task<IActionResult> Index(...) { ... }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int? id) { ... }

    [Authorize(Roles = "Admin")]
    public IActionResult Create() { ... }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(...) { ... }

    // ... same for Edit, Delete
}
```

**Pattern:**
- Class-level `[Authorize(Roles = "Admin,Librarian")]` = default: only those two roles.
- `[AllowAnonymous]` on `Index`/`Details` opens them to everyone.
- `[Authorize(Roles = "Admin")]` on `Create`/`Edit`/`Delete` tightens further.

### 9.4 Conditional links in views

In `Views/Library/Details.cshtml`:

```html
@if (User.IsInRole("Admin"))
{
    <a id="add-book" asp-action="AddBook" asp-route-id="@Model.Id" class="btn btn-primary">Додади книга</a>
    <a asp-action="Edit" asp-route-id="@Model.Id" class="btn btn-secondary">Edit</a>
    <a asp-action="Delete" asp-route-id="@Model.Id" class="btn btn-danger">Delete</a>
}
```

**Learning point — `User.IsInRole`:** the `User` property is available in all Razor views (it's the current `ClaimsPrincipal`). `IsInRole` checks role claims on the user's auth cookie.

---

## Common pitfalls & debugging tips

### `NullReferenceException` rendering a view
You forgot an `.Include()`. Navigation properties are `null` unless eagerly loaded.

### Form posts but model is empty
Usually `[Bind(...)]` is excluding your field, or the form field `name` attribute doesn't match the property name. Check the rendered HTML.

### Validation never triggers
- Missing `<span asp-validation-for="X">` — error renders nowhere.
- Missing `_ValidationScriptsPartial` in `@section Scripts {}` — no client-side validation.
- Property has no validator attribute.

### `Migration X has already been applied`
You ran `Update-Database` twice. Safe — just continue.

### Identity pages 404
The starter includes Identity pages via `app.MapRazorPages()` — keep that line.

### Tabulator shows empty table
- Check browser console — likely the AJAX URL is wrong or returning HTML (login redirect).
- Check the network tab — is `/Member/Api` returning JSON with the right field names? Tabulator's `field` must match the JSON key exactly (case-sensitive).

### "The instance of entity type cannot be tracked because another instance with the same key value is already being tracked"
You loaded an entity, then created a new one with the same Id. Use `_context.Update(model)` carefully, or detach entities you don't intend to modify.

### Cyrillic shows as `???`
Source file encoding — make sure your `.cshtml` and `.cs` files are saved as UTF-8. Visual Studio: File → Advanced Save Options → Unicode (UTF-8 with signature).

---

## Final checklist

Run through this before submitting.

### Task 1: CRUD
- [ ] `LibraryController`, `BookController`, `MemberController` exist with Create/Edit/Details/Delete
- [ ] Book cover renders as `<img>` (max-width 150px) in Book/Index and Book/Details
- [ ] Library name in Library/Index links to Details
- [ ] Library/Index has "Број на книги" column

### Task 2: Menu
- [ ] `_Layout.cshtml` has Библиотеки / Книги / Членови links (already done in starter)

### Task 3: Validation
- [ ] `Member.FirstName` and `LastName` `[Required]`
- [ ] `Member.PhoneNumber` `[RegularExpression(@"^\d{9}$")]`
- [ ] `Member.Email` `[Display(Name = "Е-адреса")]`

### Task 4: Filter
- [ ] Two inputs `name="Name"` and `name="City"` on Library/Index
- [ ] Filtered results returned by GET form
- [ ] Values persist after filtering (via `ViewBag`)

### Task 5: Tabulator
- [ ] `/Member/Api` returns JSON of all members
- [ ] `/Member/Tabulator` initializes a Tabulator
- [ ] Columns: FirstName, LastName, Е-адреса, PhoneNumber, MembershipDate, Details link
- [ ] Details link uses a `formatter` function

### Task 6: Library Details
- [ ] "Додади книга" link with `id="add-book"`
- [ ] `AddBook` action creates book under that library
- [ ] After save, redirects to Library/Details
- [ ] Books table with `id="books-table"`
- [ ] Columns: Title, Author, Genre, image (width 80px)
- [ ] Per-row Details link (`class="details-btn"`) and Borrow link (`class="borrow-btn"`)

### Task 7: Borrowing
- [ ] `Book/Borrow/{id}` GET shows form with `#book-title`, `#library-name`, members dropdown
- [ ] POST creates `BookBorrowing` with `BorrowDate = DateTime.Now`, `ReturnDate = null`, redirects to Book/Details
- [ ] Member/Details shows borrowings table with Title, BorrowDate, ReturnDate (or "Не е вратена"), "Врати" button (`class="return-btn"`)
- [ ] `Members/ReturnBook/{borrowingId}` POST sets ReturnDate, redirects to Member/Details
- [ ] Book/Details shows current (unreturned) borrowings with Member name + BorrowDate

### Task 8: Authorization
- [ ] Three roles in DB: User, Librarian, Admin
- [ ] Three users seeded with password `Password1@`
- [ ] Anonymous + User: only Index/Details for Library and Book
- [ ] Admin + Librarian: full access
- [ ] Only Admin: Create/Edit/Delete on Library and Book
- [ ] Library/Details: Edit/Delete/Add Book links only visible to Admin

---

## Resources

- [ASP.NET Core MVC docs](https://learn.microsoft.com/en-us/aspnet/core/mvc/overview)
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)
- [Tabulator.js](https://tabulator.info/)
- [Data Annotations attributes](https://learn.microsoft.com/en-us/dotnet/api/system.componentmodel.dataannotations)
- [ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity)
