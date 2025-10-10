using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Web.Services;

namespace ErpSystem.Api.Services.Maintenance;

public class MaintenanceBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MaintenanceBackgroundService> _logger;
    private readonly TimeSpan _processingInterval = TimeSpan.FromMinutes(30); // Run every 30 minutes

    public MaintenanceBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<MaintenanceBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Maintenance background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessMaintenanceTasks(stoppingToken);
                await Task.Delay(_processingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Maintenance background service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during maintenance processing");
                
                // Wait shorter time on error to retry sooner
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task ProcessMaintenanceTasks(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        
        var workOrderService = scope.ServiceProvider.GetRequiredService<IWorkOrderService>();
        var scheduleService = scope.ServiceProvider.GetRequiredService<IMaintenanceScheduleService>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        
        _logger.LogDebug("Processing maintenance tasks...");

        // Process overdue work orders
        await ProcessOverdueWorkOrders(workOrderService, emailService, cancellationToken);

        // Create work orders from schedules
        await CreateScheduledWorkOrders(scheduleService, cancellationToken);

        // Send maintenance reminders
        await SendMaintenanceReminders(workOrderService, emailService, cancellationToken);

        _logger.LogDebug("Completed maintenance task processing");
    }

    private async Task ProcessOverdueWorkOrders(
        IWorkOrderService workOrderService,
        IEmailService emailService,
        CancellationToken cancellationToken)
    {
        try
        {
            var overdueWorkOrders = await workOrderService.GetOverdueWorkOrdersAsync();
            
            if (overdueWorkOrders.Any())
            {
                _logger.LogInformation("Found {Count} overdue work orders", overdueWorkOrders.Count());
                
                // Group by assigned technician for batch notifications
                var groupedByTechnician = overdueWorkOrders
                    .Where(wo => wo.AssignedTechnicianId.HasValue)
                    .GroupBy(wo => wo.AssignedTechnicianId.Value);

                foreach (var technicianGroup in groupedByTechnician)
                {
                    await SendOverdueNotification(
                        emailService, 
                        technicianGroup.Key, 
                        technicianGroup.ToList().Cast<dynamic>().ToList(),
                        cancellationToken);
                }

                // Handle unassigned overdue work orders
                var unassignedOverdue = overdueWorkOrders
                    .Where(wo => !wo.AssignedTechnicianId.HasValue)
                    .ToList();

                if (unassignedOverdue.Any())
                {
                    _logger.LogWarning("Found {Count} unassigned overdue work orders", unassignedOverdue.Count);
                    await SendUnassignedOverdueNotification(emailService, unassignedOverdue.Cast<dynamic>().ToList(), cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing overdue work orders");
        }
    }

    private async Task CreateScheduledWorkOrders(
        IMaintenanceScheduleService scheduleService,
        CancellationToken cancellationToken)
    {
        try
        {
            var dueSchedules = await scheduleService.GetSchedulesDueForCreationAsync();
            
            foreach (var schedule in dueSchedules)
            {
                try
                {
                    var createdWorkOrders = await scheduleService.CreateWorkOrdersFromScheduleAsync(schedule.Id);
                    
                    if (createdWorkOrders.Any())
                    {
                        _logger.LogInformation("Created {Count} work orders from schedule {ScheduleId}", 
                            createdWorkOrders.Count(), schedule.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating work orders from schedule {ScheduleId}", schedule.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing scheduled work order creation");
        }
    }

    private async Task SendMaintenanceReminders(
        IWorkOrderService workOrderService,
        IEmailService emailService,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get work orders due within next 24 hours (1 day)
            var upcomingWorkOrders = await workOrderService.GetWorkOrdersDueSoonAsync(1);
            
            if (upcomingWorkOrders.Any())
            {
                var groupedByTechnician = upcomingWorkOrders
                    .Where(wo => wo.AssignedTechnicianId.HasValue)
                    .GroupBy(wo => wo.AssignedTechnicianId.Value);

                foreach (var technicianGroup in groupedByTechnician)
                {
                    await SendUpcomingWorkOrderReminder(
                        emailService,
                        technicianGroup.Key,
                        technicianGroup.ToList().Cast<dynamic>().ToList(),
                        cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending maintenance reminders");
        }
    }

    private async Task SendOverdueNotification(
        IEmailService emailService,
        Guid technicianId,
        IList<dynamic> overdueWorkOrders,
        CancellationToken cancellationToken)
    {
        try
        {
            var subject = $"Overdue Work Orders Alert - {overdueWorkOrders.Count} items";
            var body = $@"
                <h2>Overdue Work Orders</h2>
                <p>You have {overdueWorkOrders.Count} overdue work order(s):</p>
                <ul>
                {string.Join("", overdueWorkOrders.Select(wo => 
                    $"<li>WO-{wo.WorkOrderNumber}: {wo.Title} (Due: {wo.ScheduledEndDate:yyyy-MM-dd})</li>"))}
                </ul>
                <p>Please review and update these work orders as soon as possible.</p>";

            // Note: In a real implementation, you would need to get the technician's email
            // from the user service or technician service
            var technicianEmail = $"technician-{technicianId}@company.com"; // Placeholder
            
            await emailService.SendEmailAsync(technicianEmail, subject, body);
            
            _logger.LogDebug("Sent overdue notification to technician {TechnicianId}", technicianId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending overdue notification to technician {TechnicianId}", technicianId);
        }
    }

    private async Task SendUnassignedOverdueNotification(
        IEmailService emailService,
        IList<dynamic> unassignedOverdue,
        CancellationToken cancellationToken)
    {
        try
        {
            var subject = $"Unassigned Overdue Work Orders - {unassignedOverdue.Count} items";
            var body = $@"
                <h2>Unassigned Overdue Work Orders</h2>
                <p>There are {unassignedOverdue.Count} unassigned overdue work order(s):</p>
                <ul>
                {string.Join("", unassignedOverdue.Select(wo => 
                    $"<li>WO-{wo.WorkOrderNumber}: {wo.Title} (Due: {wo.ScheduledEndDate:yyyy-MM-dd})</li>"))}
                </ul>
                <p>These work orders require immediate attention and technician assignment.</p>";

            // Send to maintenance manager or admin
            var managerEmail = "maintenance.manager@company.com"; // Should come from configuration
            
            await emailService.SendEmailAsync(managerEmail, subject, body);
            
            _logger.LogDebug("Sent unassigned overdue notification to manager");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending unassigned overdue notification");
        }
    }

    private async Task SendUpcomingWorkOrderReminder(
        IEmailService emailService,
        Guid technicianId,
        IList<dynamic> upcomingWorkOrders,
        CancellationToken cancellationToken)
    {
        try
        {
            var subject = $"Upcoming Work Orders Reminder - {upcomingWorkOrders.Count} items";
            var body = $@"
                <h2>Upcoming Work Orders</h2>
                <p>You have {upcomingWorkOrders.Count} work order(s) scheduled for the next 24 hours:</p>
                <ul>
                {string.Join("", upcomingWorkOrders.Select(wo => 
                    $"<li>WO-{wo.WorkOrderNumber}: {wo.Title} (Scheduled: {wo.ScheduledStartDate:yyyy-MM-dd HH:mm})</li>"))}
                </ul>
                <p>Please ensure you have the necessary tools and materials ready.</p>";

            // Note: In a real implementation, you would get the technician's email
            var technicianEmail = $"technician-{technicianId}@company.com"; // Placeholder
            
            await emailService.SendEmailAsync(technicianEmail, subject, body);
            
            _logger.LogDebug("Sent upcoming work order reminder to technician {TechnicianId}", technicianId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending upcoming work order reminder to technician {TechnicianId}", technicianId);
        }
    }
}