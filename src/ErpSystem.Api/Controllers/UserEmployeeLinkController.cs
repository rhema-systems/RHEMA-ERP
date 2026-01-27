using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class UserEmployeeLinkController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly ILogger<UserEmployeeLinkController> _logger;

        public UserEmployeeLinkController(
            UserManager<ApplicationUser> userManager,
            IEmployeeRepository employeeRepository,
            ILogger<UserEmployeeLinkController> logger)
        {
            _userManager = userManager;
            _employeeRepository = employeeRepository;
            _logger = logger;
        }

        /// <summary>
        /// Links a user account to an employee record
        /// </summary>
        [HttpPost("link-user-to-employee")]
        public async Task<IActionResult> LinkUserToEmployee([FromBody] LinkUserEmployeeRequest request)
        {
            try
            {
                // Get the user
                var user = await _userManager.FindByIdAsync(request.UserId.ToString());
                if (user == null)
                {
                    return NotFound($"User with ID {request.UserId} not found");
                }

                // Get the employee
                var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId);
                if (employee == null)
                {
                    return NotFound($"Employee with ID {request.EmployeeId} not found");
                }

                // Check if employee is already linked to another user
                var existingUser = await _userManager.Users
                    .Where(u => u.EmployeeId == request.EmployeeId)
                    .FirstOrDefaultAsync();

                if (existingUser != null && existingUser.Id != user.Id)
                {
                    return BadRequest($"Employee {employee.FullName} is already linked to user {existingUser.UserName}");
                }

                // Link the user to the employee
                user.EmployeeId = request.EmployeeId;
                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Successfully linked user {UserName} to employee {EmployeeName}",
                        user.UserName, employee.FullName);

                    return Ok(new
                    {
                        Success = true,
                        Message = $"Successfully linked user {user.UserName} to employee {employee.FullName}",
                        UserId = user.Id,
                        EmployeeId = employee.Id,
                        EmployeeName = employee.FullName
                    });
                }

                return BadRequest(result.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error linking user {UserId} to employee {EmployeeId}", request.UserId, request.EmployeeId);
                return StatusCode(500, "An error occurred while linking user to employee");
            }
        }

        /// <summary>
        /// Removes the link between a user and employee
        /// </summary>
        [HttpPost("unlink-user-from-employee")]
        public async Task<IActionResult> UnlinkUserFromEmployee([FromBody] UnlinkUserEmployeeRequest request)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(request.UserId.ToString());
                if (user == null)
                {
                    return NotFound($"User with ID {request.UserId} not found");
                }

                user.EmployeeId = null;
                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Successfully unlinked user {UserName} from employee", user.UserName);

                    return Ok(new
                    {
                        Success = true,
                        Message = $"Successfully unlinked user {user.UserName} from employee",
                        UserId = user.Id
                    });
                }

                return BadRequest(result.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlinking user {UserId} from employee", request.UserId);
                return StatusCode(500, "An error occurred while unlinking user from employee");
            }
        }

        /// <summary>
        /// Gets all users and their linked employees
        /// </summary>
        [HttpGet("user-employee-links")]
        public async Task<IActionResult> GetUserEmployeeLinks()
        {
            try
            {
                var users = await _userManager.Users.ToListAsync();
                var links = new List<UserEmployeeLink>();

                foreach (var user in users)
                {
                    Employee? employee = null;
                    if (user.EmployeeId.HasValue)
                    {
                        employee = await _employeeRepository.GetByIdAsync(user.EmployeeId.Value);
                    }

                    links.Add(new UserEmployeeLink
                    {
                        UserId = user.Id,
                        UserName = user.UserName!,
                        FullName = user.FullName,
                        Email = user.Email!,
                        EmployeeId = user.EmployeeId,
                        EmployeeName = employee?.FullName,
                        EmployeeNumber = employee?.EmployeeNumber,
                        IsLinked = user.EmployeeId.HasValue
                    });
                }

                return Ok(links.OrderBy(l => l.UserName));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user-employee links");
                return StatusCode(500, "An error occurred while retrieving user-employee links");
            }
        }
    }

    public class LinkUserEmployeeRequest
    {
        public Guid UserId { get; set; }
        public Guid EmployeeId { get; set; }
    }

    public class UnlinkUserEmployeeRequest
    {
        public Guid UserId { get; set; }
    }

    public class UserEmployeeLink
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public Guid? EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public string? EmployeeNumber { get; set; }
        public bool IsLinked { get; set; }
    }
}
