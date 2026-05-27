
using System.Diagnostics;
using BookBorrowingSystem.Models;
using Microsoft.AspNetCore.Mvc;

namespace BookBorrowingSystem.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Error()
    {
        return View(new ErrorViewModel()
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}

