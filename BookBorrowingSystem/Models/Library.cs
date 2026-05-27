using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace LibraryApplication.Models;

public class Library
{
    public int Id { get; set; }
    
    [Required]
    [DisplayName("Library Name")]
    public string Name { get; set; }
    
    [MaxLength(200)]
    public string Address { get; set; }
    
    [StringLength(200, MinimumLength = 3)]
    public string City { get; set; }
    
    public string Country { get; set; }
    
    [Range(0, 10)]
    public double Rating { get; set; }
    
    public ICollection<Book> Books { get; set; } = new List<Book>();
}
