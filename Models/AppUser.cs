// Models/AppUser.cs
public class AppUser
{
    public int Id { get; set; }
    public string Login { get; set; }
    public string Password { get; set; }
    public string FullName { get; set; }
    public string Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastLoginDate { get; set; }
}

