using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BookBorrowingSystem.Data;
using LibraryApplication.Models;
using Microsoft.AspNetCore.Authorization;
using NuGet.LibraryModel;
using Library = LibraryApplication.Models.Library;

namespace BookBorrowingSystem.Controllers
{
    [Authorize(Roles = "Admin,Librarian")]
    public class LibraryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LibraryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Library
        [AllowAnonymous]
        public async Task<IActionResult> Index(string? name, string? city)
        {
            var query = _context.Libraries
                .Include(l => l.Books)
                .AsQueryable();
            if (!string.IsNullOrWhiteSpace(name)) {
                query = query.Where(l => l.Name.Contains(name));
            }

            if (!string.IsNullOrWhiteSpace(city)) {
                query = query.Where(l => l.City.Contains(city));
            }

            ViewBag.Name = name;
            ViewBag.City = city;

            return View(await query.ToListAsync());
        }

        // GET: Library/Details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var library = await _context.Libraries
                .Include(l => l.Books)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (library == null)
            {
                return NotFound();
            }

            return View(library);
        }

        // GET: Library/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Library/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("Id,Name,Address,City,Country,Rating")] Library library)
        {
            if (ModelState.IsValid)
            {
                _context.Add(library);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(library);
        }

        // GET: Library/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var library = await _context.Libraries.FindAsync(id);
            if (library == null)
            {
                return NotFound();
            }

            return View(library);
        }

        // POST: Library/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Address,City,Country,Rating")] Library library)
        {
            if (id != library.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(library);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LibraryExists(library.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                return RedirectToAction(nameof(Index));
            }

            return View(library);
        }

        // GET: Library/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var library = await _context.Libraries
                .FirstOrDefaultAsync(m => m.Id == id);
            if (library == null)
            {
                return NotFound();
            }

            return View(library);
        }

        // POST: Library/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var library = await _context.Libraries.FindAsync(id);
            if (library != null)
            {
                _context.Libraries.Remove(library);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool LibraryExists(int id)
        {
            return _context.Libraries.Any(e => e.Id == id);
        }

        public IActionResult AddBook(int? libraryId) {
            ViewBag.LibraryId = libraryId;
            return View();
        }

        [HttpPost] [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBook(int libraryId,
            [Bind("Title, Author, Genre, CoverImageUrl, TotalCopies")]
            Book book) {
            book.LibraryId = libraryId;
            if (ModelState.IsValid) {
                _context.Add(book);
                await _context.SaveChangesAsync();
                return RedirectToAction("Details", new {
                    id = libraryId
                });
            }

            ViewBag.LibraryId = libraryId;
            return View(book);
        }
    }
}