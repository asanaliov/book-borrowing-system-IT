using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BookBorrowingSystem.Data;
using LibraryApplication.Models;
using Microsoft.AspNetCore.Authorization;

namespace BookBorrowingSystem.Controllers
{
    [Authorize(Roles = "Admin,Librarian")]

    public class BookController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BookController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Book
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Books.Include(b => b.Library);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Book/Details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var book = await _context.Books
                .Include(b => b.Library)
                .Include(b => b.Borrowings.Where(br => br.ReturnDate == null))
                .ThenInclude(br => br.Member)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (book == null)
            {
                return NotFound();
            }

            return View(book);
        }

        // GET: Book/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            ViewData["LibraryId"] = new SelectList(_context.Libraries, "Id", "Name");
            return View();
        }

        // POST: Book/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("Id,Title,Author,Genre,CoverImageUrl,TotalCopies,LibraryId")] Book book)
        {
            if (ModelState.IsValid)
            {
                _context.Add(book);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["LibraryId"] = new SelectList(_context.Libraries, "Id", "Name", book.LibraryId);
            return View(book);
        }
        
        // GET: Book/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                return NotFound();
            }
            ViewData["LibraryId"] = new SelectList(_context.Libraries, "Id", "Name", book.LibraryId);
            return View(book);
        }

        // POST: Book/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Author,Genre,CoverImageUrl,TotalCopies,LibraryId")] Book book)
        {
            if (id != book.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(book);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BookExists(book.Id))
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
            ViewData["LibraryId"] = new SelectList(_context.Libraries, "Id", "Name", book.LibraryId);
            return View(book);
        }

        // GET: Book/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var book = await _context.Books
                .Include(b => b.Library)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (book == null)
            {
                return NotFound();
            }

            return View(book);
        }

        // POST: Book/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book != null)
            {
                _context.Books.Remove(book);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool BookExists(int id)
        {
            return _context.Books.Any(e => e.Id == id);
        }

        public async Task<IActionResult> Borrow(int id) {
            var book = await _context.Books
                .Include(b => b.Library)
                .FirstOrDefaultAsync(b => b.Id.Equals(id));
            if (book == null) return NotFound();

            ViewBag.Members = new SelectList(_context.Members, "Id", "FirstName");
            return View(book);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Borrow(int bookId, int memberId) {
            var borrowing = new BookBorrowing() {
                BookId = bookId,
                MemberId = memberId,
                BorrowDate = DateTime.Now,
                ReturnDate = null
            };
            _context.BookBorrowings.Add(borrowing);
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = bookId });
        }
        
    }
}
