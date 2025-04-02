// Controllers/AdminController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly IUserService _userService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IUserService userService, ILogger<AdminController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetUsers()
    {
        var users = _userService.GetAllUsers();
        return PartialView("_AdminPanel", users);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddUser([FromBody] AppUser user)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        user.IsActive = true;
        user.CreatedDate = DateTime.Now;

        if (_userService.AddUser(user))
        {
            _logger.LogInformation($"User {user.Login} added successfully");
            return Ok();
        }

        _logger.LogWarning($"Failed to add user {user.Login}");
        return BadRequest("User already exists or could not be added");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteUser([FromBody] DeleteUserModel model)
    {
        if (_userService.RemoveUser(model.Login))
        {
            _logger.LogInformation($"User {model.Login} deleted successfully");
            return Ok();
        }

        _logger.LogWarning($"Failed to delete user {model.Login}");
        return BadRequest("Failed to delete user");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateRole([FromBody] UpdateRoleModel model)
    {
        if (_userService.UpdateUserRole(model.Login, model.Role))
        {
            _logger.LogInformation($"Role updated for user {model.Login} to {model.Role}");
            return Ok();
        }

        _logger.LogWarning($"Failed to update role for user {model.Login}");
        return BadRequest("Failed to update role");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ActivateUser([FromBody] ActivateUserModel model)
    {
        if (_userService.ActivateUser(model.Login))
        {
            _logger.LogInformation($"User {model.Login} activated successfully");
            return Ok();
        }

        _logger.LogWarning($"Failed to activate user {model.Login}");
        return BadRequest("Failed to activate user");
    }
}

public class UpdateRoleModel
{
    public string Login { get; set; }
    public string Role { get; set; }
}

public class DeleteUserModel
{
    public string Login { get; set; }
}

public class ActivateUserModel
{
    public string Login { get; set; }
}