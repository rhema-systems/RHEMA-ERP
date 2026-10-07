using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.HR.Payroll;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Sales;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Centralized resolver for workflow entity display info (numbers, names, and navigation URLs).
/// Keeps notification/audit UX consistent across modules.
/// </summary>
public class WorkflowEntityDisplayService : IWorkflowEntityDisplayService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPurchaseRequisitionRepository _purchaseRequisitionRepository;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly ITenderRepository _tenderRepository;
    private readonly IProcurementPlanRepository _procurementPlanRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;
    private readonly IJobCardRepository _jobCardRepository;
    private readonly ErpSystem.Core.Interfaces.Inventory.IInventoryTransferRepository _inventoryTransferRepository;
    private readonly ErpSystem.Core.Interfaces.Inventory.IInventoryRequisitionRepository _inventoryRequisitionRepository;
    private readonly ILogger<WorkflowEntityDisplayService> _logger;

    public WorkflowEntityDisplayService(
        IUnitOfWork unitOfWork,
        IPurchaseRequisitionRepository purchaseRequisitionRepository,
        IPurchaseOrderRepository purchaseOrderRepository,
        ITenderRepository tenderRepository,
        IProcurementPlanRepository procurementPlanRepository,
        IProjectRepository projectRepository,
        IBusinessPartnerRepository businessPartnerRepository,
        IJobCardRepository jobCardRepository,
        ErpSystem.Core.Interfaces.Inventory.IInventoryTransferRepository inventoryTransferRepository,
        ErpSystem.Core.Interfaces.Inventory.IInventoryRequisitionRepository inventoryRequisitionRepository,
        ILogger<WorkflowEntityDisplayService> logger)
    {
        _unitOfWork = unitOfWork;
        _purchaseRequisitionRepository = purchaseRequisitionRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _tenderRepository = tenderRepository;
        _procurementPlanRepository = procurementPlanRepository;
        _projectRepository = projectRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _jobCardRepository = jobCardRepository;
        _inventoryTransferRepository = inventoryTransferRepository;
        _inventoryRequisitionRepository = inventoryRequisitionRepository;
        _logger = logger;
    }

    public async Task<WorkflowEntityDisplayInfo> GetEntityDisplayInfoAsync(string entityType, Guid entityId)
    {
        var info = new WorkflowEntityDisplayInfo
        {
            EntityType = entityType ?? string.Empty,
            EntityId = entityId
        };

        if (string.IsNullOrWhiteSpace(entityType) || entityId == Guid.Empty)
        {
            return info;
        }

        // Normalize for matching (keep original EntityType for output unless we can canonicalize it).
        var key = Normalize(entityType);

        try
        {
            if (key == Normalize("PurchaseRequisition") || key == Normalize("PURCHASE_REQUISITION") || key == Normalize("PR"))
            {
                var pr = await _purchaseRequisitionRepository.GetRequisitionByIdAsync(entityId);
                info.EntityType = "PurchaseRequisition";
                info.EntityNumber = pr?.RequisitionNumber;
                info.EntityName = pr?.Department;
                info.ActionUrl = $"/procurement/purchase-requisitions/{entityId}";
                return info;
            }

            if (key == Normalize("PurchaseOrder") || key == Normalize("PURCHASE_ORDER") || key == Normalize("PO"))
            {
                var po = await _purchaseOrderRepository.GetPurchaseOrderByIdAsync(entityId);
                info.EntityType = "PurchaseOrder";
                info.EntityNumber = po?.OrderNumber;
                if (po?.BusinessPartnerId != Guid.Empty)
                {
                    var bp = await _businessPartnerRepository.GetByIdAsync(po.BusinessPartnerId);
                    info.EntityName = bp?.PartnerName;
                }
                info.ActionUrl = $"/procurement/purchase-orders/{entityId}";
                return info;
            }

            if (key == Normalize("InventoryTransfer") || key == Normalize("INVENTORY_TRANSFER") || key == Normalize("Transfer"))
            {
                var transfer = await _inventoryTransferRepository.GetByIdAsync(entityId);
                info.EntityType = "InventoryTransfer";
                info.EntityNumber = transfer?.TransferNumber;
                info.ActionUrl = $"/inventory/transfers";
                return info;
            }

            if (key == Normalize("PurchaseReturn") || key == Normalize("INVENTORY_SUPPLIER_RETURN"))
            {
                var purchaseReturn = await _unitOfWork.Repository<PurchaseReturn>()
                    .FirstOrDefaultAsync(value => value.Id == entityId && !value.IsDeleted);
                info.EntityType = "PurchaseReturn";
                info.EntityNumber = purchaseReturn?.ReturnNumber;
                info.EntityName = purchaseReturn?.SupplierName;
                info.ActionUrl = "/inventory/supplier-returns";
                return info;
            }

            if (key == Normalize("InventoryRequisition") || key == Normalize("INVENTORY_REQUISITION"))
            {
                var req = await _inventoryRequisitionRepository.GetByIdAsync(entityId);
                info.EntityType = "InventoryRequisition";
                info.EntityNumber = req?.RequisitionNumber;
                info.ActionUrl = $"/inventory/requisitions";
                return info;
            }

            if (key == Normalize("JobCard") || key == Normalize("JOB_CARD") || key == Normalize("Job Card"))
            {
                var jobCard = await _jobCardRepository.GetByIdAsync(entityId);
                info.EntityType = "JobCard";
                info.EntityNumber = jobCard?.JobCardNumber;
                info.ActionUrl = $"/maintenance/job-cards?id={entityId}";
                return info;
            }

            if (key == Normalize("PayrollRun") || key == Normalize("PAYROLL_RUN") || key == Normalize("Payroll Run"))
            {
                var run = await _unitOfWork.Repository<PayrollRun>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "PayrollRun";
                info.EntityNumber = run?.RunNumber;
                info.EntityName = run == null
                    ? null
                    : $"{run.PayPeriodFrom:MMM yyyy} - {run.PayPeriodTo:MMM yyyy}";
                info.ActionUrl = $"/hr/payroll?runId={entityId}";
                return info;
            }

            if (key == Normalize("LeaveRequest") || key == Normalize("LEAVE_REQUEST") || key == Normalize("Leave Request"))
            {
                var leaveRequest = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffLeave.LeaveRequest>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "LeaveRequest";
                info.EntityNumber = leaveRequest?.RequestNumber;
                info.EntityName = leaveRequest == null
                    ? null
                    : $"{leaveRequest.StartDate:dd MMM yyyy} - {leaveRequest.EndDate:dd MMM yyyy} ({leaveRequest.TotalDays:0.##} days)";
                info.ActionUrl = $"/hr/leave/requests/{entityId}";
                return info;
            }

            if (key == Normalize("LeavePlan") || key == Normalize("LEAVE_PLAN") || key == Normalize("Leave Plan"))
            {
                var leavePlan = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffLeave.LeavePlan>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "LeavePlan";
                info.EntityName = leavePlan == null
                    ? null
                    : $"{leavePlan.Year} plan ({leavePlan.StartDate:dd MMM} - {leavePlan.EndDate:dd MMM})";
                info.ActionUrl = $"/hr/leave/plans?planId={entityId}";
                return info;
            }

            if (key == Normalize("LeaveEncashment") || key == Normalize("LEAVE_ENCASHMENT") || key == Normalize("Leave Encashment"))
            {
                var encashment = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffLeave.LeaveEncashment>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "LeaveEncashment";
                info.EntityName = encashment == null
                    ? null
                    : $"{encashment.DaysEncashed:0.##} days ({encashment.Year})";
                info.ActionUrl = $"/hr/leave/encashments?encashmentId={entityId}";
                return info;
            }

            if (key == Normalize("StaffAttendanceRegularization") || key == Normalize("STAFF_ATTENDANCE_REGULARIZATION") || key == Normalize("Attendance Regularization"))
            {
                var regularization = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffAttendance.StaffAttendanceRegularization>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "StaffAttendanceRegularization";
                info.EntityNumber = regularization?.RegularizationNumber;
                info.EntityName = regularization == null
                    ? null
                    : $"{regularization.Type} on {regularization.AttendanceDate:dd MMM yyyy}";
                info.ActionUrl = $"/hr/attendance/regularizations/{entityId}";
                return info;
            }

            // Company-schedule final closure, lane 2b (D-10): the inbox row names the event and its day,
            // and links to the event page — where Approve reaches the record (the generic inbox path does
            // not, cross-module #15).
            if (key == Normalize("CompanyEvent") || key == Normalize("COMPANY_EVENT") || key == Normalize("Company Event"))
            {
                var companyEvent = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.CompanySchedule.CompanyEvent>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "CompanyEvent";
                info.EntityNumber = companyEvent?.EventNumber;
                info.EntityName = companyEvent == null
                    ? null
                    : $"{companyEvent.EventName} on {companyEvent.StartDate:dd MMM yyyy}";
                info.ActionUrl = $"/hr/company-schedule/events/{entityId}";
                return info;
            }

            // Company-schedule final closure, lane 3b-1 (D-10): the inbox row names the room, its day and its booking,
            // and links to the booking page — where Approve reaches the record (the generic inbox path does not, #15).
            if (key == Normalize("RoomBooking") || key == Normalize("ROOM_BOOKING") || key == Normalize("Room Booking"))
            {
                var booking = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.CompanySchedule.RoomBooking>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.Room);
                info.EntityType = "RoomBooking";
                info.EntityNumber = booking?.BookingNumber;
                info.EntityName = booking == null
                    ? null
                    : $"{booking.Room?.RoomName ?? "A room"} on {booking.StartDateTime:dd MMM yyyy, HH:mm}";
                info.ActionUrl = $"/hr/company-schedule/bookings/{entityId}";
                return info;
            }

            if (key == Normalize("StaffOvertimeRequest") || key == Normalize("STAFF_OVERTIME_REQUEST") || key == Normalize("Overtime Request"))
            {
                var overtime = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffAttendance.StaffOvertimeRequest>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "StaffOvertimeRequest";
                info.EntityNumber = overtime?.RequestNumber;
                info.EntityName = overtime == null
                    ? null
                    : $"{overtime.PlannedOvertimeHours:0.##} hrs on {overtime.OvertimeDate:dd MMM yyyy}";
                info.ActionUrl = $"/hr/attendance/overtime/{entityId}";
                return info;
            }

            if (key == Normalize("RemoteWorkRequest") || key == Normalize("REMOTE_WORK_REQUEST") || key == Normalize("Remote Work Request"))
            {
                var remoteWork = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffAttendance.RemoteWorkRequest>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "RemoteWorkRequest";
                info.EntityNumber = remoteWork?.RequestNumber;
                info.EntityName = remoteWork == null
                    ? null
                    : $"{remoteWork.StartDate:dd MMM} - {remoteWork.EndDate:dd MMM yyyy} ({remoteWork.RequestedDays} days)";
                info.ActionUrl = $"/hr/attendance/remote-work/{entityId}";
                return info;
            }

            if (key == Normalize("ConsultantTimesheet") || key == Normalize("CONSULTANT_TIMESHEET") || key == Normalize("Consultant Timesheet"))
            {
                var timesheet = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffAttendance.ConsultantTimesheet>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "ConsultantTimesheet";
                info.EntityNumber = timesheet?.TimesheetNumber;
                info.EntityName = timesheet == null
                    ? null
                    : $"{timesheet.PeriodStartDate:dd MMM} - {timesheet.PeriodEndDate:dd MMM yyyy} ({timesheet.TotalHours:0.##} hrs)";
                info.ActionUrl = $"/hr/consulting/timesheets/{entityId}";
                return info;
            }

            if (key == Normalize("ProbationPeriod") || key == Normalize("PROBATION_PERIOD") || key == Normalize("Probation Period"))
            {
                // Two plain lookups rather than an Include: this file has no EF Core dependency and
                // every other resolver here reads the same way. Keeping it that way is cheaper than
                // pulling EntityFrameworkCore into the display layer for one navigation.
                var probation = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Recruitment.ProbationPeriod>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                var probationEmployee = probation == null
                    ? null
                    : await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Employee>()
                        .FirstOrDefaultAsync(e => e.Id == probation.EmployeeId);
                info.EntityType = "ProbationPeriod";
                info.EntityNumber = probationEmployee?.EmployeeNumber;
                // The approver is being asked to make someone permanent, so the name and the date
                // the probation ends are what they need before they open it. Deliberately no rating
                // or recommendation here: a notification travels further than the record.
                info.EntityName = probation == null
                    ? null
                    : $"{probationEmployee?.FullName ?? "Employee"} — probation ends {probation.CurrentEndDate:dd MMM yyyy}";
                info.ActionUrl = $"/hr/probation/{entityId}";
                return info;
            }

            if (key == Normalize("ManpowerBudget") || key == Normalize("MANPOWER_BUDGET") || key == Normalize("Manpower Budget"))
            {
                var budget = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.JobAnalysis.ManpowerBudget>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                var unit = budget?.OrganizationUnitId == null
                    ? null
                    : await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.OrganizationUnit>()
                        .FirstOrDefaultAsync(u => u.Id == budget.OrganizationUnitId);
                info.EntityType = "ManpowerBudget";
                info.EntityNumber = budget?.BudgetNumber;
                // The unit, the year and the headcount being asked for. An approver deciding a
                // manpower budget is deciding how many people a department may have, so the number
                // belongs in the notification rather than one click away.
                info.EntityName = budget == null
                    ? null
                    : $"{unit?.Name ?? "Organisation"} {budget.FiscalYear} — {budget.PlannedHeadcount} post(s)";
                info.ActionUrl = $"/hr/manpower-budgets/{entityId}";
                return info;
            }

            if (key == Normalize("JobDescription") || key == Normalize("JOB_DESCRIPTION") || key == Normalize("Job Description"))
            {
                var jobDescription = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.JobAnalysis.JobDescription>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                var jobPosition = jobDescription == null
                    ? null
                    : await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.EmployeePosition>()
                        .FirstOrDefaultAsync(p => p.Id == jobDescription.PositionId);
                info.EntityType = "JobDescription";
                info.EntityNumber = jobDescription?.JobDescriptionNumber;
                // The position and the version are what an approver needs before opening it: the
                // same job title arrives every year, and "v1" and "v4" are different decisions.
                info.EntityName = jobDescription == null
                    ? null
                    : $"{jobDescription.JobTitle} — {jobPosition?.Title ?? "position"} (v{jobDescription.VersionNumber})";
                info.ActionUrl = $"/hr/job-descriptions/{entityId}";
                return info;
            }

            // ── Round 2, lane F3 — teams and committees ──────────────────────
            //
            // ⚠ Both deep-link to the TEAM page and its tab, not to a record of their own, because
            // neither has a page of its own — a charter and an objective are read in the context of
            // the committee they belong to. The team id therefore has to be loaded here; the record
            // alone cannot produce a usable link.
            if (key == Normalize("HrTeamTermsOfReference") || key == Normalize("HR_TEAM_TERMS_OF_REFERENCE")
                || key == Normalize("HR Team Terms Of Reference"))
            {
                var terms = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.TeamTermsOfReference>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                var charteredTeam = terms == null
                    ? null
                    : await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Team>()
                        .FirstOrDefaultAsync(t => t.Id == terms.TeamId);
                info.EntityType = "HrTeamTermsOfReference";
                info.EntityNumber = terms == null ? null : $"v{terms.Version}";
                // Which committee, and which version — "v1" and "v4" of the same charter are
                // different decisions, exactly as they are for a job description.
                info.EntityName = terms == null
                    ? null
                    : $"{charteredTeam?.Name ?? "Team"} — terms of reference v{terms.Version}";
                info.ActionUrl = terms == null
                    ? null
                    : $"/administration/hr/organization/teams/{terms.TeamId}";
                return info;
            }

            if (key == Normalize("HrEmployeeSalaryChangeRequest") || key == Normalize("HR_EMPLOYEE_SALARY_CHANGE_REQUEST")
                || key == Normalize("HR Employee Salary Change Request"))
            {
                var request = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.EmployeeSalaryChangeRequest>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.Employee);
                info.EntityType = "HrEmployeeSalaryChangeRequest";
                info.EntityName = request == null
                    ? null
                    : $"Salary change ({request.Kind}) — {request.Employee?.FullName}";
                info.ActionUrl = request == null ? "/hr/employees" : $"/hr/employees/{request.EmployeeId}?tab=salary";
                return info;
            }

            if (key == Normalize("HrTeamObjective") || key == Normalize("HR_TEAM_OBJECTIVE")
                || key == Normalize("HR Team Objective"))
            {
                var objective = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.TeamObjective>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                var objectiveTeam = objective == null
                    ? null
                    : await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Team>()
                        .FirstOrDefaultAsync(t => t.Id == objective.TeamId);
                info.EntityType = "HrTeamObjective";
                info.EntityNumber = objective?.Code;
                info.EntityName = objective == null
                    ? null
                    : $"{objectiveTeam?.Name ?? "Team"} — {objective.Title}";
                info.ActionUrl = objective == null
                    ? null
                    : $"/administration/hr/organization/teams/{objective.TeamId}";
                return info;
            }

            if (key == Normalize("StaffMovement") || key == Normalize("STAFF_MOVEMENT") || key == Normalize("Staff Movement"))
            {
                var movement = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.PromotionTransfer.StaffMovement>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "StaffMovement";
                info.EntityNumber = movement?.MovementNumber;
                // An approver needs to know what kind of move it is and when it lands before they
                // open it — those two decide whether it is theirs to worry about today.
                info.EntityName = movement == null
                    ? null
                    : $"{movement.MovementType} effective {movement.EffectiveDate:dd MMM yyyy}";
                info.ActionUrl = $"/hr/movements/{entityId}";
                return info;
            }

            if (key == Normalize("StaffTravelRequest") || key == Normalize("STAFF_TRAVEL_REQUEST") || key == Normalize("Staff Travel Request"))
            {
                var travel = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffTravel.StaffTravelRequest>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "StaffTravelRequest";
                info.EntityNumber = travel?.RequestNumber;
                // Route and dates, because that is what decides whether an approver must act today:
                // travel approved after the departure date is worthless. The purpose is left out on
                // purpose — see the notification templates for why.
                info.EntityName = travel == null
                    ? null
                    : $"{travel.OriginCity} to {travel.DestinationCity}, {travel.TravelStartDate:dd MMM yyyy}";
                // /hr/travel/requests/{id} never existed — the approver's desk
                // detail is /hr/travel/{id}.
                info.ActionUrl = $"/hr/travel/{entityId}";
                return info;
            }

            if (key == Normalize("StaffDisciplinaryAction") || key == Normalize("STAFF_DISCIPLINARY_ACTION") || key == Normalize("Staff Disciplinary Action"))
            {
                var disciplinaryCase = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.StaffDiscipline.StaffDisciplinaryAction>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "StaffDisciplinaryAction";
                info.EntityNumber = disciplinaryCase?.CaseNumber;
                // Severity and the proposed sanction, and deliberately NOT the employee's name or
                // the allegation. This string travels into notification subjects and approval
                // queues, which are seen more widely than the case itself — an approver opens the
                // record to learn who it concerns.
                info.EntityName = disciplinaryCase == null
                    ? null
                    : $"{disciplinaryCase.Severity} disciplinary decision";
                info.ActionUrl = $"/hr/discipline/{entityId}";
                return info;
            }

            if (key == Normalize("AppraisalTemplate") || key == Normalize("APPRAISAL_TEMPLATE") || key == Normalize("Appraisal Template"))
            {
                var template = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Performance.AppraisalTemplate>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "AppraisalTemplate";
                info.EntityName = template?.TemplateName;
                info.ActionUrl = $"/administration/hr/performance/templates/{entityId}";
                return info;
            }

            // The three below withhold the employee's name, as discipline does (performance closure F8, D-95): a pay
            // change, a promotion or a termination, and an improvement plan are about one person, and these strings
            // travel into approval queues seen more widely than the record. The approver opens it to learn who.
            if (key == Normalize("SalaryReviewProposal") || key == Normalize("SALARY_REVIEW_PROPOSAL") || key == Normalize("Salary Review Proposal"))
            {
                var proposal = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Performance.SalaryReviewProposal>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "SalaryReviewProposal";
                info.EntityName = proposal == null
                    ? null
                    : $"{InWords(proposal.ProposalType.ToString())} proposal";
                info.ActionUrl = $"/hr/performance/proposals/salary-review/{entityId}";
                return info;
            }

            if (key == Normalize("EmploymentActionProposal") || key == Normalize("EMPLOYMENT_ACTION_PROPOSAL") || key == Normalize("Employment Action Proposal"))
            {
                var proposal = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Performance.EmploymentActionProposal>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "EmploymentActionProposal";
                info.EntityName = proposal == null
                    ? null
                    : $"{InWords(proposal.ActionType.ToString())} proposal";
                info.ActionUrl = $"/hr/performance/proposals/employment-action/{entityId}";
                return info;
            }

            if (key == Normalize("PerformanceImprovementPlan") || key == Normalize("PERFORMANCE_IMPROVEMENT_PLAN") || key == Normalize("Performance Improvement Plan"))
            {
                var plan = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Performance.PerformanceImprovementPlan>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "PerformanceImprovementPlan";
                // The plan's number is its reference — notifications name the record by it.
                info.EntityNumber = plan?.PipNumber;
                info.EntityName = plan == null ? null : "Performance improvement plan";
                info.ActionUrl = $"/hr/performance/pip/{entityId}";
                return info;
            }

            if (key == Normalize("StaffRequisition") || key == Normalize("STAFF_REQUISITION") || key == Normalize("Staff Requisition"))
            {
                var requisition = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Requisition.StaffRequisition>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.Position);
                info.EntityType = "StaffRequisition";
                info.EntityName = requisition == null
                    ? null
                    : $"{requisition.RequisitionNumber} — {requisition.RequisitionTitle}";
                info.ActionUrl = $"/hr/recruitment/requisitions/{entityId}";
                return info;
            }

            if (key == Normalize("EmployeeSeparation") || key == Normalize("EMPLOYEE_SEPARATION") || key == Normalize("Employee Separation"))
            {
                var separation = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.EmployeeSeparation>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.Employee);
                info.EntityType = "EmployeeSeparation";
                info.EntityNumber = separation?.SeparationNumber;
                // ⚠ The PERSON is the subject of an exit, so their name belongs in what the signatory
                // reads in their queue. "SEP-2026-0031" alone says nothing about who is leaving, and
                // this is a signature that ends somebody's employment — the one approval in the
                // inbox that should never be given without knowing whose it is.
                info.EntityName = separation == null
                    ? null
                    : separation.Employee == null
                        ? separation.SeparationNumber
                        : $"{separation.SeparationNumber} — {separation.Employee.FirstName} {separation.Employee.LastName}";
                info.ActionUrl = $"/hr/separations/{entityId}";
                return info;
            }

            // ⚠ FULLY QUALIFIED, and it has to be: this file carries
            // `using ErpSystem.Core.Entities.Finance.FixedAssets`, so a bare `AssetTransfer` here is
            // Finance's, and the key "AssetTransfer" is already claimed by it further down. HR's
            // staff-asset records answer to `HrAssetRequisition` / `HrAssetTransfer` for exactly
            // that reason — the three-level name collision.
            if (key == Normalize("HrAssetRequisition") || key == Normalize("HR_ASSET_REQUISITION") || key == Normalize("HR Asset Requisition"))
            {
                var requisition = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Assets.AssetRequisition>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.AssetType, x => x.RequestedBy);
                info.EntityType = "HrAssetRequisition";
                info.EntityNumber = requisition?.RequisitionNumber;
                // What is being asked for and by whom, because an approver deciding "yes, issue it"
                // needs both: "REQ-20260823-A1B2C3" alone says nothing about whether a laptop for a
                // new starter is a reasonable thing to sign.
                info.EntityName = requisition == null
                    ? null
                    : requisition.AssetType == null
                        ? requisition.RequisitionNumber
                        : $"{requisition.RequisitionNumber} — {requisition.Quantity} × {requisition.AssetType.Name}";
                info.ActionUrl = $"/hr/assets/requisitions/{entityId}";
                return info;
            }

            if (key == Normalize("HrAssetTransfer") || key == Normalize("HR_ASSET_TRANSFER") || key == Normalize("HR Asset Transfer"))
            {
                var transfer = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Assets.AssetTransfer>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.Asset);
                info.EntityType = "HrAssetTransfer";
                info.EntityNumber = transfer?.TransferNumber;
                info.EntityName = transfer == null
                    ? null
                    : transfer.Asset == null
                        ? transfer.TransferNumber
                        : $"{transfer.TransferNumber} — {transfer.Asset.AssetName}";
                info.ActionUrl = $"/hr/assets/transfers/{entityId}";
                return info;
            }

            if (key == Normalize("HrAssetSurcharge") || key == Normalize("HR_ASSET_SURCHARGE") || key == Normalize("HR Asset Surcharge"))
            {
                var surcharge = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Assets.AssetSurcharge>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.Employee);
                info.EntityType = "HrAssetSurcharge";
                info.EntityNumber = surcharge?.SurchargeNumber;
                // Who is being charged and how much, because that is the whole of what an approver
                // is being asked to sign. A reference alone would make a decision about somebody's
                // pay look like a filing action.
                info.EntityName = surcharge == null
                    ? null
                    : surcharge.Employee == null
                        ? surcharge.SurchargeNumber
                        : $"{surcharge.SurchargeNumber} — {surcharge.Employee.FirstName} {surcharge.Employee.LastName}, "
                          + $"{surcharge.CurrencyCode} {surcharge.AssessedAmount:0.00}";
                info.ActionUrl = $"/hr/assets/surcharges/{entityId}";
                return info;
            }

            if (key == Normalize("SuccessionPlan") || key == Normalize("SUCCESSION_PLAN") || key == Normalize("Succession Plan"))
            {
                var plan = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.SuccessionPlanning.SuccessionPlan>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.Position);
                info.EntityType = "SuccessionPlan";
                info.EntityNumber = plan?.PlanNumber;
                // The position is the subject of a succession plan, so it belongs in the name an
                // approver reads in their queue — "SP-2026-0007" alone says nothing about what is
                // being approved.
                info.EntityName = plan == null
                    ? null
                    : $"{plan.PlanNumber} — {plan.Position?.Title ?? plan.PlanName}";
                info.ActionUrl = $"/hr/succession/{entityId}";
                return info;
            }

            if (key == Normalize("JobOffer") || key == Normalize("JOB_OFFER") || key == Normalize("Job Offer"))
            {
                var offer = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Recruitment.JobOffer>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.Application);
                info.EntityType = "JobOffer";
                info.EntityNumber = offer?.OfferNumber;
                info.EntityName = offer == null
                    ? null
                    : $"{offer.OfferNumber} — {offer.PositionTitle}";
                info.ActionUrl = $"/hr/recruitment/offers/{entityId}";
                return info;
            }

            if (key == Normalize("TrainingNomination") || key == Normalize("TRAINING_NOMINATION") || key == Normalize("Training Nomination"))
            {
                var nomination = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Training.TrainingNomination>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.Employee, x => x.Schedule);
                string? nomProgramName = null;
                if (nomination?.Schedule != null)
                {
                    var nomProgram = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Training.TrainingProgram>()
                        .FirstOrDefaultAsync(p => p.Id == nomination.Schedule.ProgramId);
                    nomProgramName = nomProgram?.ProgramName;
                }
                info.EntityType = "TrainingNomination";
                info.EntityNumber = nomination?.NominationNumber;
                info.EntityName = nomination == null
                    ? null
                    : $"{nomination.Employee?.FullName} — {nomProgramName ?? nomination.Schedule?.ScheduleNumber}";
                // The old "/hr/training/nominations?nominationId=" pointed at a
                // route that never existed. The reader of a workflow item is the APPROVER, so it
                // lands on the desk nomination detail.
                info.ActionUrl = $"/hr/training/nominations/{entityId}";
                return info;
            }

            if (key == Normalize("FleetTrip") || key == Normalize("FLEET_TRIP") || key == Normalize("Fleet Trip"))
            {
                var trip = await _unitOfWork.Repository<ErpSystem.Core.Entities.Maintenance.FleetTrip>()
                    .FirstOrDefaultAsync(t => t.Id == entityId, t => t.VehicleAsset);
                info.EntityType = "FleetTrip";
                info.EntityNumber = $"FT-{entityId.ToString()[..8].ToUpperInvariant()}";
                info.EntityName = trip?.VehicleAsset?.Name;
                info.ActionUrl = $"/maintenance/fleet/trips?id={entityId}";
                return info;
            }

            if (key == Normalize("Tender") || key == Normalize("TENDER") || key == Normalize("ProcurementTender"))
            {
                var tender = await _tenderRepository.GetByIdAsync(entityId);
                info.EntityType = "Tender";
                info.EntityNumber = tender?.TenderNumber;
                info.EntityName = tender?.Title;
                info.ActionUrl = $"/procurement/tenders/{entityId}";
                return info;
            }

            if (key == Normalize("FleetTripInspection") || key == Normalize("FLEET_TRIP_INSPECTION") || key == Normalize("Fleet Trip Inspection"))
            {
                var inspection = await _unitOfWork.Repository<ErpSystem.Core.Entities.Maintenance.FleetTripInspection>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.VehicleAsset, x => x.InspectionTemplate);
                info.EntityType = "FleetTripInspection";
                info.EntityNumber = $"FI-{entityId.ToString()[..8].ToUpperInvariant()}";
                info.EntityName = inspection == null
                    ? null
                    : $"{inspection.VehicleAsset?.Name ?? "Asset"} / {inspection.InspectionTemplate?.Name ?? inspection.InspectionKind}";
                info.ActionUrl = inspection?.FleetTripId.HasValue == true
                    ? $"/maintenance/fleet/trips?id={inspection.FleetTripId}&inspectionId={entityId}"
                    : $"/maintenance/assets?id={inspection?.VehicleAssetId}&tab=inspections&inspectionId={entityId}";
                return info;
            }

            if (key == Normalize("ProcurementPlan") || key == Normalize("PROCUREMENT_PLAN") || key == Normalize("Procurement Plan"))
            {
                var plan = await _procurementPlanRepository.GetWithFullDetailsAsync(entityId);
                info.EntityType = "ProcurementPlan";
                info.EntityNumber = plan?.PlanNumber;
                info.EntityName = plan == null
                    ? null
                    : string.IsNullOrWhiteSpace(plan.Department?.Name)
                        ? plan.Title
                        : $"{plan.Title} ({plan.Department.Name})";
                info.ActionUrl = $"/procurement/planning/plans/{entityId}";
                return info;
            }

            if (key == Normalize("Project") || key == Normalize("PROJECT"))
            {
                var project = await _projectRepository.GetByIdAsync(entityId);
                info.EntityType = "Project";
                info.EntityNumber = project?.ProjectCode;
                info.EntityName = project?.Title;
                info.ActionUrl = $"/development/projects/{entityId}";
                return info;
            }

            if (key == Normalize("ProjectDeliverable") || key == Normalize("PROJECT_DELIVERABLE") || key == Normalize("Project Deliverable"))
            {
                var deliverable = await _unitOfWork.Repository<ErpSystem.Core.Entities.Projects.ProjectDeliverable>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.Project);
                info.EntityType = "ProjectDeliverable";
                info.EntityNumber = deliverable?.Project?.ProjectCode;
                info.EntityName = deliverable == null
                    ? null
                    : $"{deliverable.Project?.Title ?? "Project"} / {deliverable.Title}";
                info.ActionUrl = deliverable == null ? null : $"/development/projects/{deliverable.ProjectId}";
                return info;
            }

            if (key == Normalize("ProjectClosure") || key == Normalize("PROJECT_CLOSURE") || key == Normalize("Project Closure"))
            {
                var closure = await _unitOfWork.Repository<ErpSystem.Core.Entities.Projects.ProjectClosure>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.Project);
                info.EntityType = "ProjectClosure";
                info.EntityNumber = closure?.Project?.ProjectCode;
                info.EntityName = closure == null
                    ? null
                    : $"{closure.Project?.Title ?? "Project"} / Closure";
                info.ActionUrl = closure == null ? null : $"/development/projects/{closure.ProjectId}";
                return info;
            }

            // Finance approval notifications depend on these entity links, so keep them with the newer Sales workflow mappings below.
            if (key == Normalize("AccountingBookLifecycle"))
            {
                var book = await _unitOfWork.Repository<AccountingBook>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "AccountingBookLifecycle";
                info.EntityNumber = book?.Code;
                info.EntityName = book?.Name;
                info.ActionUrl = "/finance/settings/accounting-books";
                return info;
            }

            if (key == Normalize("AccountingBookPeriodLifecycle"))
            {
                var period = await _unitOfWork.Repository<AccountingBookPeriod>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.AccountingBook);
                info.EntityType = "AccountingBookPeriodLifecycle";
                info.EntityNumber = period?.AccountingBook?.Code;
                info.EntityName = period == null
                    ? null
                    : $"{period.AccountingBook?.Name ?? "Accounting book"} period {period.PeriodStatus}";
                info.ActionUrl = period == null
                    ? "/finance/settings/accounting-books"
                    : $"/finance/settings/accounting-books/{period.AccountingBookId:D}/readiness";
                return info;
            }

            if (key == Normalize("AccountingBookInitialization"))
            {
                var initialization = await _unitOfWork.Repository<AccountingBookInitialization>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.AccountingBook);
                info.EntityType = "AccountingBookInitialization";
                info.EntityNumber = initialization == null
                    ? null
                    : $"{initialization.AccountingBook?.Code}/V{initialization.Version}";
                info.EntityName = initialization?.AccountingBook?.Name;
                info.ActionUrl = initialization == null
                    ? "/finance/settings/accounting-books"
                    : $"/finance/settings/accounting-books/{initialization.AccountingBookId:D}/readiness";
                return info;
            }

            // Historical policy workflows remain readable after policy authoring is retired,
            // but they route to the accounting-book register rather than a configuration page.
            if (key == Normalize("AccountingBookApplicabilityPolicy"))
            {
                var policy = await _unitOfWork.Repository<AccountingBookApplicabilityPolicy>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "AccountingBookApplicabilityPolicy";
                info.EntityNumber = policy == null ? null : $"{policy.PolicyCode}/V{policy.Version}";
                info.EntityName = policy?.Name;
                info.ActionUrl = "/finance/settings/accounting-books";
                return info;
            }

            if (key == Normalize("JournalEntry"))
            {
                var journal = await _unitOfWork.Repository<JournalEntry>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "JournalEntry";
                info.EntityNumber = journal?.JournalEntryNumber;
                info.EntityName = journal?.Description;
                info.ActionUrl = $"/finance/journal-entries/{entityId}";
                return info;
            }

            if (key == Normalize("FinancePurchaseOrder"))
            {
                var po = await _unitOfWork.Repository<FinancePurchaseOrder>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.Vendor);
                info.EntityType = "FinancePurchaseOrder";
                info.EntityNumber = po?.OrderNumber;
                info.EntityName = po?.Vendor?.PartnerName;
                info.ActionUrl = $"/finance/ap/purchase-orders/{entityId}";
                return info;
            }

            if (key == Normalize("FinancePurchaseOrderReceipt"))
            {
                var receipt = await _unitOfWork.Repository<FinancePurchaseOrderReceipt>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.FinancePurchaseOrder);
                info.EntityType = "FinancePurchaseOrderReceipt";
                info.EntityNumber = receipt?.ReceiptNumber;
                info.EntityName = receipt?.FinancePurchaseOrder?.OrderNumber;
                info.ActionUrl = $"/finance/ap/receipts/{entityId}";
                return info;
            }

            if (key == Normalize("VendorInvoice"))
            {
                var invoice = await _unitOfWork.Repository<VendorInvoice>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "VendorInvoice";
                info.EntityNumber = invoice?.InvoiceNumber;
                info.EntityName = invoice?.SupplierName;
                info.ActionUrl = $"/finance/ap/invoices/{entityId}";
                return info;
            }

            if (key == Normalize("VendorPayment"))
            {
                var payment = await _unitOfWork.Repository<VendorPayment>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.BusinessPartner);
                info.EntityType = "VendorPayment";
                info.EntityNumber = payment?.PaymentNumber;
                info.EntityName = payment?.BusinessPartner?.PartnerName;
                info.ActionUrl = $"/finance/ap/payments/{entityId}";
                return info;
            }

            if (key == Normalize("PaymentBatch"))
            {
                var batch = await _unitOfWork.Repository<PaymentBatch>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "PaymentBatch";
                info.EntityNumber = batch?.BatchNumber;
                info.EntityName = batch?.Description;
                info.ActionUrl = $"/finance/ap/payments";
                return info;
            }

            if (key == Normalize("SupplierReturn"))
            {
                var supplierReturn = await _unitOfWork.Repository<SupplierReturn>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.Vendor);
                info.EntityType = "SupplierReturn";
                info.EntityNumber = supplierReturn?.ReturnNumber;
                info.EntityName = supplierReturn?.VendorName ?? supplierReturn?.Vendor?.PartnerName;
                info.ActionUrl = $"/finance/ap/returns/{entityId}";
                return info;
            }

            if (key == Normalize("JournalBatch"))
            {
                var batch = await _unitOfWork.Repository<JournalBatch>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "JournalBatch";
                info.EntityNumber = batch?.BatchNumber;
                info.EntityName = batch?.Description;
                info.ActionUrl = $"/finance/journal-batches/{entityId}";
                return info;
            }

            if (key == Normalize("Quote"))
            {
                var quote = await _unitOfWork.Repository<Quote>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "Quote";
                info.EntityNumber = quote?.DocumentNumber;
                info.EntityName = quote?.QuoteName;
                info.ActionUrl = $"/sales/crm/quotes";
                return info;
            }

            if (key == Normalize("SalesOrder") || key == Normalize("SALES_ORDER") || key == Normalize("Sales Order"))
            {
                var order = await _unitOfWork.Repository<SalesOrder>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "SalesOrder";
                info.EntityNumber = order?.DocumentNumber;
                info.EntityName = order?.CustomerName;
                info.ActionUrl = $"/sales/orders/{entityId}";
                return info;
            }

            if (key == Normalize("DeliveryNote"))
            {
                var delivery = await _unitOfWork.Repository<DeliveryNote>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "DeliveryNote";
                info.EntityNumber = delivery?.DocumentNumber;
                info.EntityName = delivery?.CustomerName;
                info.ActionUrl = $"/sales/deliveries";
                return info;
            }

            if (key == Normalize("Invoice"))
            {
                var invoice = await _unitOfWork.Repository<Invoice>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "Invoice";
                info.EntityNumber = invoice?.InvoiceNumber;
                info.EntityName = invoice?.CustomerName;
                info.ActionUrl = $"/finance/ar/invoices/{entityId}";
                return info;
            }

            if (key == Normalize("ReturnOrder"))
            {
                // Return workflows use the canonical BusinessPartner identity so workflow cards agree with AR.
                var returnOrder = await _unitOfWork.Repository<ReturnOrder>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.BusinessPartner);
                info.EntityType = "ReturnOrder";
                info.EntityNumber = returnOrder?.DocumentNumber;
                info.EntityName = returnOrder?.BusinessPartner?.PartnerName;
                info.ActionUrl = $"/sales/return-orders";
                return info;
            }

            if (key == Normalize("CreditNote"))
            {
                var creditNote = await _unitOfWork.Repository<CreditNote>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.BusinessPartner);
                info.EntityType = "CreditNote";
                info.EntityNumber = creditNote?.DocumentNumber;
                info.EntityName = creditNote?.BusinessPartner?.PartnerName;
                info.ActionUrl = $"/sales/credit-notes";
                return info;
            }

            if (key == Normalize("CustomerPayment"))
            {
                var payment = await _unitOfWork.Repository<CustomerPayment>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.BusinessPartner);
                info.EntityType = "CustomerPayment";
                info.EntityNumber = payment?.PaymentNumber;
                info.EntityName = payment?.BusinessPartnerName;
                info.ActionUrl = $"/finance/ar/payments";
                return info;
            }

            if (key == Normalize("Refund"))
            {
                var refund = await _unitOfWork.Repository<Refund>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.BusinessPartner);
                info.EntityType = "Refund";
                info.EntityNumber = refund?.DocumentNumber;
                info.EntityName = refund?.BusinessPartner?.PartnerName;
                info.ActionUrl = $"/sales/refunds";
                return info;
            }

            if (key == Normalize("BudgetScenario"))
            {
                var scenario = await _unitOfWork.Repository<BudgetScenario>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "BudgetScenario";
                info.EntityNumber = scenario?.Name;
                info.EntityName = scenario?.Status;
                info.ActionUrl = $"/finance/budgeting/scenarios/{entityId}";
                return info;
            }

            if (key == Normalize("BudgetReturn"))
            {
                var budgetReturn = await _unitOfWork.Repository<BudgetReturn>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.BudgetScenario);
                info.EntityType = "BudgetReturn";
                info.EntityNumber = budgetReturn?.BudgetScenario?.Name ?? entityId.ToString()[..8].ToUpperInvariant();
                info.EntityName = budgetReturn?.Status;
                info.ActionUrl = $"/finance/budgeting/returns/{entityId}";
                return info;
            }

            if (key == Normalize("BudgetRevision"))
            {
                var revision = await _unitOfWork.Repository<BudgetRevision>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.SourceScenario);
                info.EntityType = "BudgetRevision";
                info.EntityNumber = revision?.RevisionNumber;
                info.EntityName = revision == null
                    ? null
                    : $"{revision.RevisionType} - {revision.SourceScenario?.Name}";
                info.ActionUrl = $"/finance/budgeting/revisions/{entityId}";
                return info;
            }

            if (key == Normalize("UnitJournalEntry"))
            {
                var entry = await _unitOfWork.Repository<UnitJournalEntry>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "UnitJournalEntry";
                info.EntityNumber = entry?.EntryNumber;
                info.EntityName = entry?.Description;
                info.ActionUrl = $"/finance/unit-journal-entries/{entityId}";
                return info;
            }

            if (key == Normalize("UnitAccountBudget"))
            {
                var budget = await _unitOfWork.Repository<UnitAccountBudget>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.UnitAccount);
                info.EntityType = "UnitAccountBudget";
                info.EntityNumber = budget?.BudgetVersion;
                info.EntityName = budget?.UnitAccount?.Name;
                info.ActionUrl = $"/finance/unit-budgets/{entityId}";
                return info;
            }

            if (key == Normalize("AllocationRule"))
            {
                var allocation = await _unitOfWork.Repository<AllocationRule>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "AllocationRule";
                info.EntityNumber = allocation?.Code;
                info.EntityName = allocation?.Name;
                info.ActionUrl = $"/finance/allocations/{entityId}";
                return info;
            }

            if (key == Normalize("CashTransaction"))
            {
                var transaction = await _unitOfWork.Repository<CashTransaction>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "CashTransaction";
                info.EntityNumber = transaction?.TransactionNumber;
                info.EntityName = transaction?.Description ?? transaction?.PayeeOrPayer;
                info.ActionUrl = $"/finance/cash/transactions";
                return info;
            }

            if (key == Normalize("BankReconciliation"))
            {
                var reconciliation = await _unitOfWork.Repository<BankReconciliation>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.BankAccount);
                info.EntityType = "BankReconciliation";
                info.EntityNumber = reconciliation == null ? null : $"REC-{reconciliation.ReconciliationDate:yyyyMMdd}";
                info.EntityName = reconciliation?.BankAccount?.AccountName;
                info.ActionUrl = $"/finance/cash/reconciliation";
                return info;
            }

            if (key == Normalize("BankDepositBatch") || key == Normalize("Bank Deposit"))
            {
                var deposit = await _unitOfWork.Repository<BankDepositBatch>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.BankAccount);
                info.EntityType = "BankDepositBatch";
                info.EntityNumber = deposit?.DepositNumber;
                info.EntityName = deposit == null
                    ? null
                    : $"{deposit.BankAccount?.AccountName} / {deposit.Currency} {deposit.NetAmount:N2}";
                info.ActionUrl = $"/finance/cash/deposits/{entityId}";
                return info;
            }

            if (key == Normalize("ReturnedChequeCase") || key == Normalize("Returned Cheque"))
            {
                var returnedCheque = await _unitOfWork.Repository<ReturnedChequeCase>()
                    .FirstOrDefaultAsync(x => x.Id == entityId, x => x.BankAccount);
                info.EntityType = "ReturnedChequeCase";
                info.EntityNumber = returnedCheque?.CaseNumber;
                info.EntityName = returnedCheque == null
                    ? null
                    : $"Cheque {returnedCheque.ChequeNumber} / {returnedCheque.BankAccount?.AccountName}";
                info.ActionUrl = $"/finance/cash/returned-cheques?caseId={entityId}";
                return info;
            }

            if (key == Normalize("Cheque"))
            {
                var cheque = await _unitOfWork.Repository<Cheque>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "Cheque";
                info.EntityNumber = cheque?.ChequeNumber;
                info.EntityName = cheque?.PayeeName;
                info.ActionUrl = $"/finance/cash/transactions";
                return info;
            }

            if (key == Normalize("FixedAsset"))
            {
                var asset = await _unitOfWork.Repository<FixedAsset>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "FixedAsset";
                info.EntityNumber = asset?.AssetCode;
                info.EntityName = asset?.Name;
                info.ActionUrl = $"/finance/fixed-assets/register/{entityId}";
                return info;
            }

            if (key == Normalize("AssetDepreciationSchedule"))
            {
                var schedule = await _unitOfWork.Repository<AssetDepreciationSchedule>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.FixedAsset);
                info.EntityType = "AssetDepreciationSchedule";
                info.EntityNumber = schedule?.FixedAsset?.AssetCode;
                info.EntityName = schedule == null ? null : $"Depreciation {schedule.DepreciationAmount:N2}";
                info.ActionUrl = $"/finance/fixed-assets/depreciation";
                return info;
            }

            if (key == Normalize("AssetValuation"))
            {
                var valuation = await _unitOfWork.Repository<AssetValuation>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.FixedAsset);
                info.EntityType = "AssetValuation";
                info.EntityNumber = valuation?.ValuationReportReference ?? valuation?.FixedAsset?.AssetCode;
                info.EntityName = valuation?.FixedAsset?.Name;
                info.ActionUrl = $"/finance/fixed-assets/valuations";
                return info;
            }

            if (key == Normalize("AssetTransfer"))
            {
                var transfer = await _unitOfWork.Repository<AssetTransfer>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.FixedAsset);
                info.EntityType = "AssetTransfer";
                info.EntityNumber = transfer?.ReferenceNumber ?? transfer?.FixedAsset?.AssetCode;
                info.EntityName = transfer?.ToLocation;
                info.ActionUrl = $"/finance/fixed-assets/transfers";
                return info;
            }

            if (key == Normalize("AssetDisposal"))
            {
                var disposal = await _unitOfWork.Repository<AssetDisposal>().FirstOrDefaultAsync(x => x.Id == entityId, x => x.FixedAsset);
                info.EntityType = "AssetDisposal";
                info.EntityNumber = disposal?.ReferenceNumber ?? disposal?.FixedAsset?.AssetCode;
                info.EntityName = disposal?.Reason;
                info.ActionUrl = $"/finance/fixed-assets/disposals";
                return info;
            }

            if (key == Normalize("AssetVerificationSession"))
            {
                var session = await _unitOfWork.Repository<AssetVerificationSession>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "AssetVerificationSession";
                info.EntityNumber = session?.ReferenceNumber;
                info.EntityName = session?.SessionName;
                info.ActionUrl = $"/finance/fixed-assets/verification/{entityId}";
                return info;
            }

            if (key == Normalize("CapitalProject"))
            {
                var capitalProject = await _unitOfWork.Repository<CapitalProject>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "CapitalProject";
                info.EntityNumber = capitalProject?.ProjectCode;
                info.EntityName = capitalProject?.Name;
                info.ActionUrl = $"/finance/fixed-assets/capital-projects/{entityId}";
                return info;
            }

            if (key == Normalize("LeaseContract"))
            {
                var lease = await _unitOfWork.Repository<LeaseContract>().FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "LeaseContract";
                info.EntityNumber = lease?.ContractNumber;
                info.EntityName = lease?.Description;
                info.ActionUrl = $"/finance/fixed-assets/leases/{entityId}";
                return info;
            }

            // Newer Sales workflow entities are preserved alongside Finance display links for shared approval screens.
            if (key == Normalize("SalesAgreement") || key == Normalize("SALES_AGREEMENT") || key == Normalize("Sales Agreement"))
            {
                var agreement = await _unitOfWork.Repository<SalesAgreement>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "SalesAgreement";
                info.EntityNumber = agreement?.DocumentNumber;
                info.EntityName = agreement == null
                    ? null
                    : string.IsNullOrWhiteSpace(agreement.CustomerName)
                        ? agreement.AgreementTitle
                        : $"{agreement.AgreementTitle} ({agreement.CustomerName})";
                info.ActionUrl = $"/sales/agreements/{entityId}";
                return info;
            }

            if (key == Normalize("SalesAllocation") || key == Normalize("SALES_ALLOCATION") || key == Normalize("Sales Allocation") || key == Normalize("PlotAllocation"))
            {
                var allocation = await _unitOfWork.Repository<SalesAllocation>()
                    .FirstOrDefaultAsync(x => x.Id == entityId);
                info.EntityType = "SalesAllocation";
                info.EntityNumber = allocation?.SourceItemCode ?? allocation?.SourceItemId;
                info.EntityName = allocation == null
                    ? null
                    : string.IsNullOrWhiteSpace(allocation.CustomerName)
                        ? allocation.SourceItemName
                        : $"{allocation.SourceItemName} ({allocation.CustomerName})";
                info.ActionUrl = $"/sales/allocations/{entityId}";
                return info;
            }

            if (key == Normalize("BusinessPartner") || key == Normalize("BUSINESS_PARTNER") || key == Normalize("Business Partner") || key == Normalize("Supplier"))
            {
                var bp = await _businessPartnerRepository.GetByIdAsync(entityId);
                info.EntityType = "BusinessPartner";
                info.EntityNumber = bp?.PartnerCode;
                info.EntityName = bp?.PartnerName;
                info.ActionUrl = $"/procurement/business-partners/{entityId}";
                return info;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to resolve entity display info for {EntityType} {EntityId}", entityType, entityId);
        }

        return info;
    }

    private static string Normalize(string s)
        => new string((s ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    /// <summary>An enum member's name as words: <c>MeritIncrease</c> → "Merit increase".</summary>
    private static string InWords(string pascal)
    {
        var words = new System.Text.StringBuilder(pascal.Length + 4);
        for (var i = 0; i < pascal.Length; i++)
        {
            var c = pascal[i];
            if (i > 0 && char.IsUpper(c))
                words.Append(' ').Append(char.ToLowerInvariant(c));
            else
                words.Append(c);
        }
        return words.ToString();
    }
}
