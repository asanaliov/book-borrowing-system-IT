using Microsoft.AspNetCore.Identity;

namespace LibraryApplication.Models;

public class LibraryApplicationUser : IdentityUser
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Address { get; set; }
}
