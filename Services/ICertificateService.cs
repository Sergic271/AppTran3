// Services/ICertificateService.cs
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

public interface ICertificateService
{
    Task<List<CertificateInfo>> GetAvailableCertificates();
    Task<string> GetCertificateFingerprint(X509Certificate2 certificate);
    Task<string> GetUserFullName(X509Certificate2 certificate);
}

