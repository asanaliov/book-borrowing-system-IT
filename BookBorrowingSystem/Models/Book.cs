using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

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
    [ValidateNever]
    public Library? Library { get; set; }
    
    public ICollection<BookBorrowing> Borrowings { get; set; } = new List<BookBorrowing>();
}
