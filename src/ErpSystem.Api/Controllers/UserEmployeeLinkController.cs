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
        private readonly ErpSystem.Api.Services.IEmployeeLinkResolutionService _linkResolution;
        private readonly ILogger<UserEmployeeLinkController> _logger;

        public UserEmployeeLinkController(
            UserManager<ApplicationUser> userManager,
            IEmployeeRepository employeeRepository,
            ErpSystem.Api.Services.IEmployeeLinkResolutionService linkResolution,
            ILogger<UserEmployeeLinkController> logger)
        {
            _userManager = userManager;
            _employeeRepository = employeeRepository;
            _linkResolution = linkResolution;
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

        /// <summary>
        /// Area 25 slice 1 — the HR unlinked-users queue. Every user with no employee link,
        /// each carrying the exact-match candidates the D5 auto-link would have used (same
        /// rules, same service), so HR sees both the suggestion and why auto-link held back
        /// (IsExact=false marks an ambiguous rule).
        /// </summary>
        [HttpGet("unlinked-users")]
        public async Task<IActionResult> GetUnlinkedUsers()
        {
            try
            {
                var users = await _userManager.Users
                    .Where(u => u.EmployeeId == null)
                    .OrderBy(u => u.UserName)
                    .ToListAsync();

                var rows = new List<UnlinkedUserRow>();
                foreach (var user in users)
                {
                    rows.Add(new UnlinkedUserRow
                    {
                        UserId = user.Id,
                        UserName = user.UserName!,
                        FullName = user.FullName,
                        Email = user.Email ?? string.Empty,
                        AuthenticationProvider = user.AuthenticationProvider.ToString(),
                        IsActive = user.IsActive,
                        LastLoginDate = user.LastLoginDate,
                        Suggestions = (await _linkResolution.SuggestForUserAsync(user)).ToList()
                    });
                }

                return Ok(rows);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unlinked users");
                return StatusCode(500, "An error occurred while retrieving unlinked users");
            }
        }

        /// <summary>
        /// Links several users in one submission (the queue's "link all exact matches").
        /// Each pair passes the same guards as the single link; one failure does not stop
        /// the rest — the per-pair outcome comes back so the queue can show what happened.
        /// </summary>
        [HttpPost("bulk-link")]
        public async Task<IActionResult> BulkLink([FromBody] BulkLinkRequest request)
        {
            if (request.Links.Count == 0)
            {
                return BadRequest("No links submitted");
            }

            var results = new List<BulkLinkResult>();
            foreach (var pair in request.Links)
            {
                try
                {
                    var user = await _userManager.FindByIdAsync(pair.UserId.ToString());
                    if (user == null)
                    {
                        results.Add(BulkLinkResult.Fail(pair, "User not found"));
                        continue;
                    }
                    if (user.EmployeeId.HasValue)
                    {
                        results.Add(BulkLinkResult.Fail(pair, "User is already linked"));
                        continue;
                    }

                    var employee = await _employeeRepository.GetByIdAsync(pair.EmployeeId);
                    if (employee == null)
                    {
                        results.Add(BulkLinkResult.Fail(pair, "Employee not found"));
                        continue;
                    }

                    var existingUser = await _userManager.Users
                        .Where(u => u.EmployeeId == pair.EmployeeId)
                        .FirstOrDefaultAsync();
                    if (existingUser != null)
                    {
                        results.Add(BulkLinkResult.Fail(pair, $"Employee is already linked to user {existingUser.UserName}"));
                        continue;
                    }

                    user.EmployeeId = pair.EmployeeId;
                    var update = await _userManager.UpdateAsync(user);
                    if (update.Succeeded)
                    {
                        _logger.LogInformation("Bulk-linked user {UserName} to employee {EmployeeName}", user.UserName, employee.FullName);
                        results.Add(new BulkLinkResult { UserId = pair.UserId, EmployeeId = pair.EmployeeId, Success = true, Message = $"Linked {user.UserName} to {employee.FullName}" });
                    }
                    else
                    {
                        results.Add(BulkLinkResult.Fail(pair, string.Join(", ", update.Errors.Select(e => e.Description))));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Bulk link failed for user {UserId} -> employee {EmployeeId}", pair.UserId, pair.EmployeeId);
                    results.Add(BulkLinkResult.Fail(pair, "Unexpected error"));
                }
            }

            return Ok(new
            {
                Linked = results.Count(r => r.Success),
                Failed = results.Count(r => !r.Success),
                Results = results
            });
        }
    }

    public class UnlinkedUserRow
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string AuthenticationProvider { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public List<ErpSystem.Api.Services.LinkSuggestion> Suggestions { get; set; } = new();
    }

    public class BulkLinkRequest
    {
        public List<LinkUserEmployeeRequest> Links { get; set; } = new();
    }

    public class BulkLinkResult
    {
        public Guid UserId { get; set; }
        public Guid EmployeeId { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        public static BulkLinkResult Fail(LinkUserEmployeeRequest pair, string message) =>
            new() { UserId = pair.UserId, EmployeeId = pair.EmployeeId, Success = false, Message = message };
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
