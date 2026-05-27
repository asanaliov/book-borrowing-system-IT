# Book Borrowing System

An ASP.NET Core MVC application for managing libraries, books, members, and book borrowings.

## Tech Stack

- ASP.NET Core MVC (`net10.0`)
- Entity Framework Core with SQLite (SQL Server package also referenced)
- ASP.NET Core Identity for authentication
- xUnit + Playwright for testing

## Project Structure

```
BookBorrowingSystem.sln
├── BookBorrowingSystem/        # Main MVC web application
│   ├── Areas/Identity/         # Identity scaffolding
│   ├── Controllers/            # MVC controllers
│   ├── Data/                   # ApplicationDbContext & migrations
│   ├── Models/                 # Domain models (Book, Library, Member, BookBorrowing)
│   ├── Views/                  # Razor views
│   └── wwwroot/                # Static assets
└── TestExamIS.Tests/           # Test project (controller + Playwright tests)
```

## Domain Models

- **Book** — `Title`, `Author`, `Genre`, `CoverImageUrl`, `TotalCopies`
- **Library** — `Name`, `Address`, `City`, `Country`, `Rating`
- **Member** — `FirstName`, `LastName`, `Email`, `PhoneNumber`, `MembershipDate`
- **BookBorrowing** — `BorrowDate`, `ReturnDate`

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Setup

1. Restore dependencies:
   ```bash
   dotnet restore
   ```

2. Apply database migrations:
   ```bash
   dotnet ef database update --project BookBorrowingSystem
   ```

3. Run the application:
   ```bash
   dotnet run --project BookBorrowingSystem
   ```

The app will be available at the URL printed in the console (typically `https://localhost:5001`).

## Configuration

The default connection string is defined in `BookBorrowingSystem/appsettings.json` under `ConnectionStrings:DefaultConnection` and uses SQLite. Override it with user-secrets or environment variables as needed.

## Running Tests

```bash
dotnet test
```

The test suite includes controller tests and Playwright-based end-to-end tests in `TestExamIS.Tests/`.