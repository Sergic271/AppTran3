// Models/ADUser.cs
public class ADUser
{
    public int Id { get; set; }
    public string DomainUsername { get; set; }
    public string FullName { get; set; }
    public string Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastLoginDate { get; set; }
}