// Services/IUserService.cs
public interface IUserService
{
    AppUser AuthenticateUser(string login, string password);
    bool AddUser(AppUser user);
    bool RemoveUser(string login);
    bool UpdateUserRole(string login, string role);
    bool UpdateUser(AppUser user);
    bool ActivateUser(string login);
    List<AppUser> GetAllUsers();
}