namespace LibraryApplication.Models;

public class Member
{
    public int Id { get; set; }
    
    public string FirstName { get; set; }
    
    public string LastName { get; set; }
    
    public string Email { get; set; }
    
    public string PhoneNumber { get; set; }
    
    public DateTime MembershipDate { get; set; }
    
    public ICollection<BookBorrowing> Borrowings { get; set; } = new List<BookBorrowing>();
}
