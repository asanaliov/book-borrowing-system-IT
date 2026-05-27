using BookBorrowingSystem.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace TestExamIS.Tests.Utils;

public static class TestWebApplicationFactoryExtensions
{
    public static WebApplicationFactory<TStartup> WithTestDatabase<TStartup>(
        this WebApplicationFactory<TStartup> factory) where TStartup : class
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

                if (descriptor != null)
                    services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
                });

                var sp = services.BuildServiceProvider();

                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Database.EnsureDeleted();
                db.Database.EnsureCreated();
                TestDatabaseHelper.SeedDatabase(db);
            });
        });
    }

    public static WebApplicationFactory<TStartup> WithTestAuth<TStartup>(
        this WebApplicationFactory<TStartup> factory) where TStartup : class
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });
            });
        });
    }

    public static HttpClient CreateAnonymousClient<T>(this WebApplicationFactory<T> factory) where T : class
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });
    }

    public static HttpClient CreateAuthenticatedClient<T>(this WebApplicationFactory<T> factory, string userType = "user") where T : class
    {
        TestAuthHandler.UserType = userType;

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost")
        });

        client.DefaultRequestHeaders.Add("Authorization", "Test");
        client.DefaultRequestHeaders.Add("Test-User", userType);

        return client;
    }
}
