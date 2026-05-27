using System.Linq.Expressions;
using BookBorrowingSystem.Data;
using LibraryApplication.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace TestExamIS.Tests.Utils;

public static class TestDatabaseHelper
{
    public static void SeedDatabase(ApplicationDbContext context)
    {
        var library1 = new Library
        {
            Name = "Градска Библиотека",
            Address = "ул. Цар Самоил 1",
            City = "Скопје",
            Country = "Македонија",
            Rating = 8.5
        };
        var library2 = new Library
        {
            Name = "Национална Библиотека",
            Address = "бул. Гоце Делчев 6",
            City = "Битола",
            Country = "Македонија",
            Rating = 9.0
        };
        var library3 = new Library
        {
            Name = "Универзитетска Библиотека",
            Address = "ул. Руѓер Бошковиќ 16",
            City = "Скопје",
            Country = "Македонија",
            Rating = 7.8
        };

        context.Libraries.AddRange(library1, library2, library3);
        context.SaveChanges();

        var book1 = new Book
        {
            Title = "На Мраз",
            Author = "Живко Чинго",
            Genre = "Роман",
            CoverImageUrl = "https://example.com/covers/na-mraz.jpg",
            TotalCopies = 5,
            LibraryId = library1.Id
        };
        var book2 = new Book
        {
            Title = "Пустина",
            Author = "Горан Стефановски",
            Genre = "Драма",
            CoverImageUrl = "https://example.com/covers/pustina.jpg",
            TotalCopies = 3,
            LibraryId = library1.Id
        };
        var book3 = new Book
        {
            Title = "Македонска крвава свадба",
            Author = "Војдан Чернодрински",
            Genre = "Драма",
            CoverImageUrl = "https://example.com/covers/svadba.jpg",
            TotalCopies = 7,
            LibraryId = library2.Id
        };

        context.Books.AddRange(book1, book2, book3);
        context.SaveChanges();

        var member1 = new Member
        {
            FirstName = "Александар",
            LastName = "Петровски",
            Email = "aleksandar@example.com",
            PhoneNumber = "070123456",
            MembershipDate = new DateTime(2023, 1, 15)
        };
        var member2 = new Member
        {
            FirstName = "Марија",
            LastName = "Стојановска",
            Email = "marija@example.com",
            PhoneNumber = "071234567",
            MembershipDate = new DateTime(2023, 3, 20)
        };
        var member3 = new Member
        {
            FirstName = "Никола",
            LastName = "Димитровски",
            Email = "nikola@example.com",
            PhoneNumber = "072345678",
            MembershipDate = new DateTime(2024, 5, 10)
        };

        context.Members.AddRange(member1, member2, member3);
        context.SaveChanges();

        // Borrowing that has NOT been returned (ReturnDate == null)
        var borrowing1 = new BookBorrowing
        {
            BookId = book1.Id,
            MemberId = member1.Id,
            BorrowDate = DateTime.Now.AddDays(-10),
            ReturnDate = null
        };
        // Borrowing that HAS been returned
        var borrowing2 = new BookBorrowing
        {
            BookId = book2.Id,
            MemberId = member1.Id,
            BorrowDate = DateTime.Now.AddDays(-30),
            ReturnDate = DateTime.Now.AddDays(-5)
        };
        // Another active borrowing for book1
        var borrowing3 = new BookBorrowing
        {
            BookId = book1.Id,
            MemberId = member2.Id,
            BorrowDate = DateTime.Now.AddDays(-3),
            ReturnDate = null
        };

        context.BookBorrowings.AddRange(borrowing1, borrowing2, borrowing3);
        context.SaveChanges();
    }

    public static async Task ResetDatabaseAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        SeedDatabase(context);
    }

    public static async Task<int> GetCount<T>(IServiceProvider serviceProvider) where T : class
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Set<T>().CountAsync();
    }

    public static async Task<T> GetFirst<T>(IServiceProvider serviceProvider) where T : class
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Set<T>().FirstAsync();
    }

    public static async Task<List<T>> GetAllWhere<T>(
        IServiceProvider services,
        Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IQueryable<T>>? include = null)
        where T : class
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        IQueryable<T> query = dbContext.Set<T>().Where(predicate);
        if (include != null)
            query = include(query);

        return await query.ToListAsync();
    }

    public static T? GetById<T>(IServiceProvider serviceProvider, Func<T, bool> predicate) where T : class
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return context.Set<T>().Where(predicate).FirstOrDefault();
    }

    public static async Task<T> CreateEntity<T>(IServiceProvider services, T entity) where T : class
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Set<T>().AddAsync(entity);
        await dbContext.SaveChangesAsync();
        return entity;
    }
}
