using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace LibraryApplication.Models;

public class BookBorrowing
{
    public int Id { get; set; }
    public int BookId { get; set; }
    [ValidateNever]
    public Book Book { get; set; }
    
    public int MemberId { get; set; }
    [ValidateNever]
    public Member Member { get; set; }
    public DateTime BorrowDate { get; set; }

    public DateTime? ReturnDate { get; set; }
}