// Models/CertificateUser.cs
public class CertificateUser
{
    public int Id { get; set; }
    public string? Fingerprint { get; set; }
    public string? FullName { get; set; }
    public string? Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastLoginDate { get; set; }
}

