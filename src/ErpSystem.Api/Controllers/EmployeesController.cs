using System.ComponentModel.DataAnnotations;
using AutoMapper;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EmployeesController : ControllerBase
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IDepartmentRepository _departmentRepository;
        private readonly IEmployeePositionRepository _positionRepository;
        private readonly IEmployeeService _employeeService;
        private readonly IMapper _mapper;
        private readonly ILogger<EmployeesController> _logger;

        public EmployeesController(
            IEmployeeRepository employeeRepository,
            IDepartmentRepository departmentRepository,
            IEmployeePositionRepository positionRepository,
            IMapper mapper,
            ILogger<EmployeesController> logger,
            IEmployeeService employeeService)
        {
            _employeeRepository = employeeRepository;
            _departmentRepository = departmentRepository;
            _positionRepository = positionRepository;
            _mapper = mapper;
            _logger = logger;
            _employeeService = employeeService;
        }

        /// <summary>
        /// Get all active employees with pagination
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<EmployeeDto>), 200)]
        public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetEmployees(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] Guid? departmentId = null,
            [FromQuery] StaffStatus? status = null)
        {
            try
            {
                IEnumerable<Employee> employees;

                if (!string.IsNullOrWhiteSpace(search))
                {
                    employees = await _employeeRepository.SearchEmployeesAsync(search);
                }
                else if (departmentId.HasValue)
                {
                    employees = await _employeeRepository.GetByDepartmentAsync(departmentId.Value);
                }
                else if (status.HasValue)
                {
                    employees = await _employeeRepository.GetByStatusAsync(status.Value);
                }
                else
                {
                    employees = await _employeeRepository.GetActiveEmployeesAsync();
                }

                // Apply pagination
                var paginatedEmployees = employees
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize);

                var employeeDtos = _mapper.Map<IEnumerable<EmployeeDto>>(paginatedEmployees);

                Response.Headers.Append("X-Total-Count", employees.Count().ToString());
                Response.Headers.Append("X-Page", page.ToString());
                Response.Headers.Append("X-Page-Size", pageSize.ToString());

                return Ok(employeeDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving employees");
                return StatusCode(500, "An error occurred while retrieving employees");
            }
        }

        /// <summary>
        /// Get employee by ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(EmployeeDetailDto), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<EmployeeDetailDto>> GetEmployee(Guid id)
        {
            try
            {
                var employee = await _employeeRepository.GetByIdAsync(id,
                    e => e.Department,
                    e => e.Position,
                    e => e.Section!,
                    e => e.Manager!,
                    e => e.Country!,
                    e => e.Shift!);

                if (employee == null)
                {
                    return NotFound($"Employee with ID {id} not found");
                }

                var employeeDto = _mapper.Map<EmployeeDetailDto>(employee);
                return Ok(employeeDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving employee {EmployeeId}", id);
                return StatusCode(500, "An error occurred while retrieving the employee");
            }
        }

        /// <summary>
        /// Get employee by employee number
        /// </summary>
        [HttpGet("by-number/{employeeNumber}")]
        [ProducesResponseType(typeof(EmployeeDetailDto), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<EmployeeDetailDto>> GetEmployeeByNumber(string employeeNumber)
        {
            try
            {
                var employee = await _employeeRepository.GetByEmployeeNumberAsync(employeeNumber);

                if (employee == null)
                {
                    return NotFound($"Employee with number {employeeNumber} not found");
                }

                var employeeDto = _mapper.Map<EmployeeDetailDto>(employee);
                return Ok(employeeDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving employee by number {EmployeeNumber}", employeeNumber);
                return StatusCode(500, "An error occurred while retrieving the employee");
            }
        }

        /// <summary>
        /// Get employees by department
        /// </summary>
        /// <param name="departmentId">Department ID</param>
        /// <returns>List of employees in the department</returns>
        /// <response code="200">Employees retrieved</response>
        [HttpGet("by-department/{departmentId}")]
        [ProducesResponseType(typeof(IEnumerable<EmployeeDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetEmployeesByDepartment(
            Guid departmentId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            IEnumerable<EmployeeDto> employees;

            employees = await _employeeService.GetByDepartmentAsync(departmentId);

            var paginatedEmployees = employees
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

            var employeeDtos = _mapper.Map<IEnumerable<EmployeeDto>>(paginatedEmployees);

            Response.Headers["X-Total-Count"] = employees.Count().ToString();
            Response.Headers["X-Page"] = page.ToString();
            Response.Headers["X-Page-Size"] = pageSize.ToString();

            return Ok(employeeDtos);
        }

        /// <summary>
        /// Create new employee
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(EmployeeDetailDto), 201)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<EmployeeDetailDto>> CreateEmployee([FromBody] CreateEmployeeDto createEmployeeDto)
        {
            try
            {
                // Validate employee number uniqueness
                if (await _employeeRepository.EmployeeNumberExistsAsync(createEmployeeDto.EmployeeNumber))
                {
                    return BadRequest("Employee number already exists");
                }

                // Validate email uniqueness
                if (await _employeeRepository.EmailExistsAsync(createEmployeeDto.EmailAddress))
                {
                    return BadRequest("Email address already exists");
                }

                // Validate department exists
                var department = await _departmentRepository.GetByIdAsync(createEmployeeDto.DepartmentId);
                if (department == null)
                {
                    return BadRequest("Department not found");
                }

                // Validate position exists
                var position = await _positionRepository.GetByIdAsync(createEmployeeDto.PositionId);
                if (position == null)
                {
                    return BadRequest("Position not found");
                }

                var employee = _mapper.Map<Employee>(createEmployeeDto);

                var createdEmployee = await _employeeRepository.AddAsync(employee);
                await _employeeRepository.SaveChangesAsync();

                // Retrieve the created employee with related data
                var employeeWithRelations = await _employeeRepository.GetByIdAsync(createdEmployee.Id,
                    e => e.Department,
                    e => e.Position,
                    e => e.Section!,
                    e => e.Manager!);

                var employeeDto = _mapper.Map<EmployeeDetailDto>(employeeWithRelations);

                return CreatedAtAction(nameof(GetEmployee), new { id = employee.Id }, employeeDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating employee");
                return StatusCode(500, "An error occurred while creating the employee");
            }
        }

        /// <summary>
        /// Update employee
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(EmployeeDetailDto), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<EmployeeDetailDto>> UpdateEmployee(Guid id, [FromBody] UpdateEmployeeDto updateEmployeeDto)
        {
            try
            {
                var existingEmployee = await _employeeRepository.GetByIdAsync(id);
                if (existingEmployee == null)
                {
                    return NotFound($"Employee with ID {id} not found");
                }

                // TODO: Implement employee number and email uniqueness validation for updates
                // Current HR interface methods don't support exclusion parameters
                // Need to either extend the interface or implement different validation logic
                //
                // For now, skip these validations during updates

                _mapper.Map(updateEmployeeDto, existingEmployee);

                await _employeeRepository.UpdateAsync(existingEmployee);
                await _employeeRepository.SaveChangesAsync();

                // Retrieve updated employee with related data
                var updatedEmployee = await _employeeRepository.GetByIdAsync(id,
                    e => e.Department,
                    e => e.Position,
                    e => e.Section!,
                    e => e.Manager!);

                var employeeDto = _mapper.Map<EmployeeDetailDto>(updatedEmployee);
                return Ok(employeeDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating employee {EmployeeId}", id);
                return StatusCode(500, "An error occurred while updating the employee");
            }
        }

        /// <summary>
        /// Delete employee (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<ActionResult> DeleteEmployee(Guid id)
        {
            try
            {
                var employee = await _employeeRepository.GetByIdAsync(id);
                if (employee == null)
                {
                    return NotFound($"Employee with ID {id} not found");
                }

                await _employeeRepository.DeleteAsync(employee);
                await _employeeRepository.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting employee {EmployeeId}", id);
                return StatusCode(500, "An error occurred while deleting the employee");
            }
        }

        /// <summary>
        /// Get employees available for maintenance assignments
        /// </summary>
        [HttpGet("maintenance-available")]
        [ProducesResponseType(typeof(IEnumerable<EmployeeDto>), 200)]
        public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetMaintenanceEmployees()
        {
            try
            {
                var employees = await _employeeRepository.GetMaintenanceTechniciansAsync();
                var employeeDtos = _mapper.Map<IEnumerable<EmployeeDto>>(employees);

                return Ok(employeeDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving maintenance employees");
                return StatusCode(500, "An error occurred while retrieving maintenance employees");
            }
        }

        /// <summary>
        /// Terminate an employee
        /// </summary>
        /// <param name="id">Employee ID</param>
        /// <param name="dto">Termination details</param>
        /// <returns>Success status</returns>
        /// <response code="200">Employee terminated successfully</response>
        /// <response code="400">Invalid request</response>
        /// <response code="404">Employee not found</response>
        [HttpPost("{id}/terminate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> TerminateEmployee(Guid id, [FromBody] TerminateEmployeeDto dto)
        {
            try
            {
                await _employeeService.TerminateEmployeeAsync(id, dto);
                return Ok(new { message = "Employee terminated successfully" });
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating employee {EmployeeId}", id);
                return StatusCode(500, "An error occurred while terminating the employee");
            }
        }

        // TODO: Uncomment when GetByManagerAsync is added to the HR interface
        // /// <summary>
        // /// Get employees by manager
        // /// </summary>
        // [HttpGet("by-manager/{managerId}")]
        // [ProducesResponseType(typeof(IEnumerable<EmployeeDto>), 200)]
        // public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetEmployeesByManager(Guid managerId)
        // {
        //     try
        //     {
        //         var employees = await _employeeRepository.GetByManagerAsync(managerId);
        //         var employeeDtos = _mapper.Map<IEnumerable<EmployeeDto>>(employees);
        //
        //         return Ok(employeeDtos);
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogError(ex, "Error retrieving employees by manager {ManagerId}", managerId);
        //         return StatusCode(500, "An error occurred while retrieving employees");
        //     }
        // }

        /// <summary>
        /// Get all active departments for dropdown selection
        /// </summary>
        [HttpGet("departments")]
        [ProducesResponseType(typeof(IEnumerable<DepartmentDto>), 200)]
        public async Task<ActionResult<IEnumerable<DepartmentDto>>> GetDepartments()
        {
            try
            {
                var departments = await _departmentRepository.GetActiveDepartmentsAsync();
                var departmentDtos = _mapper.Map<IEnumerable<DepartmentDto>>(departments);

                return Ok(departmentDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving departments");
                return StatusCode(500, "An error occurred while retrieving departments");
            }
        }
    }
}
