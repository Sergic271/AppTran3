using System.Collections.Generic;
using System.Threading.Tasks;
using AppTran.Models;

namespace AppTran.Services
{
    public interface IApplicantService
    {
        Task<List<ApplicantInfo>> SearchApplicantsAsync(string searchText);
        Task<bool> CheckRegNumberExistsAsync(string regNumber);

    }
}


