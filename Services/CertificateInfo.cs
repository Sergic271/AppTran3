using System.Security.Cryptography.X509Certificates;
/*using StoreName.My;*/

public class CertificateInfo
{
    public X509Certificate2 Certificate { get; set; }
    public string Thumbprint { get; set; }
    public string SubjectName { get; set; }
    public string INN { get; set; }
    public string Issuer { get; set; }
    public DateTime NotBefore { get; set; }
    public DateTime NotAfter { get; set; }
}