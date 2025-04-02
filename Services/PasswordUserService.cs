// Services/PasswordUserService.cs
using System.IO;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

public class PasswordUserService : IUserService
{
    private readonly string _configPath;
    private readonly ILogger _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PasswordUserService(
        IConfiguration configuration, 
        ILogger<PasswordUserService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.users");
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    private List<AppUser> ReadUsers()
    {
        try
        {
            _logger.LogInformation($"Reading from: {_configPath}");
            if (!File.Exists(_configPath))
            {
                _logger.LogWarning("Users file not found. Creating empty users list.");
                return new List<AppUser>();
            }

            var lines = File.ReadAllLines(_configPath);
            var users = new List<AppUser>();

            foreach (var line in lines)
            {
                var parts = line.Split(';');
                if (parts.Length >= 4) // Login;Password;FullName;Role
                {
                    users.Add(new AppUser
                    {
                        Id = users.Count + 1,
                        Login = parts[0],
                        Password = parts[1],
                        FullName = parts[2],
                        Role = parts[3],
                        IsActive = true,
                        CreatedDate = DateTime.Now,
                        LastLoginDate = null
                    });
                }
            }

            _logger.LogInformation($"Loaded {users.Count} users");
            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading users");
            return new List<AppUser>();
        }
    }

    private void WriteUsers(List<AppUser> users)
    {
        try
        {
            var lines = users.Select(u => $"{u.Login};{u.Password};{u.FullName};{u.Role}");
            File.WriteAllLines(_configPath, lines);
            _logger.LogInformation($"Wrote {users.Count} users to file");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing users");
        }
    }

    public AppUser AuthenticateUser(string login, string password)
    {
        var users = ReadUsers();
        var user = users.FirstOrDefault(u => 
            string.Equals(u.Login, login, StringComparison.OrdinalIgnoreCase) &&
            u.Password == password &&
            u.IsActive);

        if (user != null)
        {
            user.LastLoginDate = DateTime.Now;
            WriteUsers(users);
            _logger.LogInformation($"User {login} authenticated successfully");
        }
        else
        {
            _logger.LogWarning($"Authentication failed for {login}");
        }
        
        return user;
    }

    public bool AddUser(AppUser user)
    {
        var users = ReadUsers();
        
        // Check if user with same login already exists
        if (users.Any(u => u.Login.Equals(user.Login, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning($"User with login {user.Login} already exists");
            return false;
        }
        
        user.Id = users.Count > 0 ? users.Max(u => u.Id) + 1 : 1;
        user.IsActive = true;
        user.CreatedDate = DateTime.Now;
        user.LastLoginDate = null;
        users.Add(user);
        WriteUsers(users);
        
        _logger.LogInformation($"User {user.Login} added successfully");
        return true;
    }

    public bool RemoveUser(string login)
    {
        var users = ReadUsers();
        var userToRemove = users.FirstOrDefault(u =>
            string.Equals(u.Login, login, StringComparison.OrdinalIgnoreCase));

        if (userToRemove != null)
        {
            users.Remove(userToRemove);
            WriteUsers(users);
            _logger.LogInformation($"User {login} removed successfully");
            return true;
        }
        
        _logger.LogWarning($"Failed to remove user {login} - user not found");
        return false;
    }

    public bool UpdateUserRole(string login, string role)
    {
        var users = ReadUsers();
        var user = users.FirstOrDefault(u => 
            string.Equals(u.Login, login, StringComparison.OrdinalIgnoreCase));
            
        if (user != null)
        {
            user.Role = role;
            WriteUsers(users);
            _logger.LogInformation($"Role updated for user {login} to {role}");
            return true;
        }
        
        _logger.LogWarning($"Failed to update role for user {login} - user not found");
        return false;
    }

    public bool UpdateUser(AppUser updatedUser)
    {
        var users = ReadUsers();
        var existingUser = users.FirstOrDefault(u => 
            string.Equals(u.Login, updatedUser.Login, StringComparison.OrdinalIgnoreCase));
            
        if (existingUser != null)
        {
            existingUser.FullName = updatedUser.FullName;
            existingUser.Password = updatedUser.Password;
            existingUser.Role = updatedUser.Role;
            existingUser.IsActive = updatedUser.IsActive;
            
            WriteUsers(users);
            _logger.LogInformation($"User {updatedUser.Login} updated successfully");
            return true;
        }
        
        _logger.LogWarning($"Failed to update user {updatedUser.Login} - user not found");
        return false;
    }

    public bool ActivateUser(string login)
    {
        var users = ReadUsers();
        var user = users.FirstOrDefault(u => 
            string.Equals(u.Login, login, StringComparison.OrdinalIgnoreCase));
            
        if (user != null)
        {
            user.IsActive = true;
            WriteUsers(users);
            _logger.LogInformation($"User {login} activated successfully");
            return true;
        }
        
        _logger.LogWarning($"Failed to activate user {login} - user not found");
        return false;
    }

    public List<AppUser> GetAllUsers()
    {
        return ReadUsers();
    }
}