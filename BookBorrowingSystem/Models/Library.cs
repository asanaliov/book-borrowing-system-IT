using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace LibraryApplication.Models;

public class Library
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Address { get; set; }

    public string City { get; set; }

    public string Country { get; set; }
    public double Rating { get; set; }
    [ValidateNever] public ICollection<Book> Books { get; set; } = new List<Book>();
}