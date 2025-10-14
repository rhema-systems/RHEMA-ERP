using AutoMapper;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Api.Controllers.Maintenance;

namespace ErpSystem.Api.Mapping;

public class MaintenanceMappingProfile : Profile
{
    public MaintenanceMappingProfile()
    {
        // Asset Management Mappings
        CreateMap<MaintenanceAsset, MaintenanceAssetDto>()
            .ForMember(dest => dest.AssetCategory, opt => opt.MapFrom(src => src.AssetCategory))
            .ForMember(dest => dest.ParentAsset, opt => opt.MapFrom(src => src.ParentAsset))
            .ForMember(dest => dest.ChildAssets, opt => opt.MapFrom(src => src.ChildAssets));

        CreateMap<CreateMaintenanceAssetDto, MaintenanceAsset>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.AssetCategory, opt => opt.Ignore())
            .ForMember(dest => dest.ParentAsset, opt => opt.Ignore())
            .ForMember(dest => dest.ChildAssets, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrders, opt => opt.Ignore())
            .ForMember(dest => dest.MaintenanceSchedules, opt => opt.Ignore())
            .ForMember(dest => dest.Inspections, opt => opt.Ignore())
            .ForMember(dest => dest.Downtimes, opt => opt.Ignore());

        CreateMap<UpdateMaintenanceAssetDto, MaintenanceAsset>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.AssetNumber, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.OperatingHours, opt => opt.Ignore())
            .ForMember(dest => dest.LastOperatingHoursUpdate, opt => opt.Ignore())
            .ForMember(dest => dest.Mileage, opt => opt.Ignore())
            .ForMember(dest => dest.LastMileageUpdate, opt => opt.Ignore())
            .ForMember(dest => dest.AssetCategory, opt => opt.Ignore())
            .ForMember(dest => dest.ParentAsset, opt => opt.Ignore())
            .ForMember(dest => dest.ChildAssets, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrders, opt => opt.Ignore())
            .ForMember(dest => dest.MaintenanceSchedules, opt => opt.Ignore())
            .ForMember(dest => dest.Inspections, opt => opt.Ignore())
            .ForMember(dest => dest.Downtimes, opt => opt.Ignore());

        CreateMap<MaintenanceAssetCategory, MaintenanceAssetCategoryDto>()
            .ForMember(dest => dest.AssetCount, opt => opt.MapFrom(src => src.Assets.Count))
            .ForMember(dest => dest.ParentCategory, opt => opt.MapFrom(src => src.ParentCategory))
            .ForMember(dest => dest.ChildCategories, opt => opt.MapFrom(src => src.ChildCategories));

        CreateMap<CreateMaintenanceAssetCategoryDto, MaintenanceAssetCategory>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Assets, opt => opt.Ignore())
            .ForMember(dest => dest.ParentCategory, opt => opt.Ignore())
            .ForMember(dest => dest.ChildCategories, opt => opt.Ignore());

        CreateMap<UpdateMaintenanceAssetCategoryDto, MaintenanceAssetCategory>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Assets, opt => opt.Ignore())
            .ForMember(dest => dest.ParentCategory, opt => opt.Ignore())
            .ForMember(dest => dest.ChildCategories, opt => opt.Ignore());

        // Work Order Mappings
        CreateMap<WorkOrder, WorkOrderDto>()
            .ForMember(dest => dest.Asset, opt => opt.MapFrom(src => src.Asset))
            .ForMember(dest => dest.WorkOrderType, opt => opt.MapFrom(src => src.WorkOrderType))
            .ForMember(dest => dest.MaintenanceType, opt => opt.MapFrom(src => src.MaintenanceType))
            .ForMember(dest => dest.PriorityLevel, opt => opt.MapFrom(src => src.PriorityLevel))
            .ForMember(dest => dest.AssignedTechnician, opt => opt.MapFrom(src => src.AssignedTechnician))
            .ForMember(dest => dest.AssignedTeam, opt => opt.MapFrom(src => src.AssignedTeam))
            .ForMember(dest => dest.RequestedBy, opt => opt.MapFrom(src => src.RequestedBy))
            .ForMember(dest => dest.ApprovedBy, opt => opt.MapFrom(src => src.ApprovedBy))
            .ForMember(dest => dest.ParentWorkOrder, opt => opt.MapFrom(src => src.ParentWorkOrder))
            .ForMember(dest => dest.Tasks, opt => opt.MapFrom(src => src.Tasks))
            .ForMember(dest => dest.Parts, opt => opt.MapFrom(src => src.Parts))
            .ForMember(dest => dest.Labor, opt => opt.MapFrom(src => src.Labor))
            .ForMember(dest => dest.Comments, opt => opt.MapFrom(src => src.Comments));

        CreateMap<CreateWorkOrderDto, WorkOrder>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrderNumber, opt => opt.Ignore())
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => "Draft"))
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.ActualStartDate, opt => opt.Ignore())
            .ForMember(dest => dest.ActualCompletionDate, opt => opt.Ignore())
            .ForMember(dest => dest.ActualCost, opt => opt.Ignore())
            .ForMember(dest => dest.ActualHours, opt => opt.Ignore())
            .ForMember(dest => dest.RequestedById, opt => opt.Ignore()) // Set from current user service
            .ForMember(dest => dest.ApprovedById, opt => opt.Ignore())
            .ForMember(dest => dest.ApprovedAt, opt => opt.Ignore())
            .ForMember(dest => dest.CompletionNotes, opt => opt.Ignore())
            .ForMember(dest => dest.FailureCode, opt => opt.Ignore())
            .ForMember(dest => dest.CauseCode, opt => opt.Ignore())
            .ForMember(dest => dest.ActionCode, opt => opt.Ignore())
            .ForMember(dest => dest.IsRecurring, opt => opt.Ignore())
            .ForMember(dest => dest.CustomFields, opt => opt.Ignore())
            .ForMember(dest => dest.Asset, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrderType, opt => opt.Ignore())
            .ForMember(dest => dest.MaintenanceType, opt => opt.Ignore())
            .ForMember(dest => dest.PriorityLevel, opt => opt.Ignore())
            .ForMember(dest => dest.AssignedTechnician, opt => opt.Ignore())
            .ForMember(dest => dest.AssignedTeam, opt => opt.Ignore())
            .ForMember(dest => dest.RequestedBy, opt => opt.Ignore())
            .ForMember(dest => dest.ApprovedBy, opt => opt.Ignore())
            .ForMember(dest => dest.ParentWorkOrder, opt => opt.Ignore())
            .ForMember(dest => dest.MaintenanceSchedule, opt => opt.Ignore())
            .ForMember(dest => dest.ChildWorkOrders, opt => opt.Ignore())
            .ForMember(dest => dest.Tasks, opt => opt.Ignore())
            .ForMember(dest => dest.Parts, opt => opt.Ignore())
            .ForMember(dest => dest.Labor, opt => opt.Ignore())
            .ForMember(dest => dest.Documents, opt => opt.Ignore())
            .ForMember(dest => dest.Comments, opt => opt.Ignore());

        CreateMap<UpdateWorkOrderDto, WorkOrder>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrderNumber, opt => opt.Ignore())
            .ForMember(dest => dest.AssetId, opt => opt.Ignore())
            .ForMember(dest => dest.Status, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.ActualStartDate, opt => opt.Ignore())
            .ForMember(dest => dest.ActualCompletionDate, opt => opt.Ignore())
            .ForMember(dest => dest.ActualCost, opt => opt.Ignore())
            .ForMember(dest => dest.ActualHours, opt => opt.Ignore())
            .ForMember(dest => dest.RequestedById, opt => opt.Ignore())
            .ForMember(dest => dest.ApprovedById, opt => opt.Ignore())
            .ForMember(dest => dest.ApprovedAt, opt => opt.Ignore())
            .ForMember(dest => dest.CompletionNotes, opt => opt.Ignore())
            .ForMember(dest => dest.FailureCode, opt => opt.Ignore())
            .ForMember(dest => dest.CauseCode, opt => opt.Ignore())
            .ForMember(dest => dest.ActionCode, opt => opt.Ignore())
            .ForMember(dest => dest.ParentWorkOrderId, opt => opt.Ignore())
            .ForMember(dest => dest.MaintenanceScheduleId, opt => opt.Ignore())
            .ForMember(dest => dest.IsRecurring, opt => opt.Ignore())
            .ForMember(dest => dest.CustomFields, opt => opt.Ignore())
            .ForMember(dest => dest.Asset, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrderType, opt => opt.Ignore())
            .ForMember(dest => dest.MaintenanceType, opt => opt.Ignore())
            .ForMember(dest => dest.PriorityLevel, opt => opt.Ignore())
            .ForMember(dest => dest.AssignedTechnician, opt => opt.Ignore())
            .ForMember(dest => dest.AssignedTeam, opt => opt.Ignore())
            .ForMember(dest => dest.RequestedBy, opt => opt.Ignore())
            .ForMember(dest => dest.ApprovedBy, opt => opt.Ignore())
            .ForMember(dest => dest.ParentWorkOrder, opt => opt.Ignore())
            .ForMember(dest => dest.MaintenanceSchedule, opt => opt.Ignore())
            .ForMember(dest => dest.ChildWorkOrders, opt => opt.Ignore())
            .ForMember(dest => dest.Tasks, opt => opt.Ignore())
            .ForMember(dest => dest.Parts, opt => opt.Ignore())
            .ForMember(dest => dest.Labor, opt => opt.Ignore())
            .ForMember(dest => dest.Documents, opt => opt.Ignore())
            .ForMember(dest => dest.Comments, opt => opt.Ignore());

        CreateMap<WorkOrderType, WorkOrderTypeDto>();
        CreateMap<CreateWorkOrderTypeDto, WorkOrderType>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrders, opt => opt.Ignore());

        CreateMap<UpdateWorkOrderTypeDto, WorkOrderType>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrders, opt => opt.Ignore());

        CreateMap<MaintenanceType, MaintenanceTypeDto>();
        CreateMap<CreateMaintenanceTypeDto, MaintenanceType>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrders, opt => opt.Ignore());

        CreateMap<UpdateMaintenanceTypeDto, MaintenanceType>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrders, opt => opt.Ignore());

        CreateMap<PriorityLevel, PriorityLevelDto>();
        CreateMap<CreatePriorityLevelDto, PriorityLevel>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrders, opt => opt.Ignore());

        CreateMap<UpdatePriorityLevelDto, PriorityLevel>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrders, opt => opt.Ignore());

        // Work Order Detail Mappings
        CreateMap<WorkOrderTask, WorkOrderTaskDto>()
            .ForMember(dest => dest.AssignedTechnician, opt => opt.MapFrom(src => src.AssignedTechnician));

        CreateMap<CreateWorkOrderTaskDto, WorkOrderTask>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => "Pending"))
            .ForMember(dest => dest.ActualHours, opt => opt.Ignore())
            .ForMember(dest => dest.StartedAt, opt => opt.Ignore())
            .ForMember(dest => dest.CompletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.CompletionNotes, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrder, opt => opt.Ignore())
            .ForMember(dest => dest.AssignedTechnician, opt => opt.Ignore());

        CreateMap<UpdateWorkOrderTaskDto, WorkOrderTask>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrderId, opt => opt.Ignore())
            .ForMember(dest => dest.Status, opt => opt.Ignore())
            .ForMember(dest => dest.ActualHours, opt => opt.Ignore())
            .ForMember(dest => dest.StartedAt, opt => opt.Ignore())
            .ForMember(dest => dest.CompletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.CompletionNotes, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrder, opt => opt.Ignore())
            .ForMember(dest => dest.AssignedTechnician, opt => opt.Ignore());

        CreateMap<WorkOrderPart, WorkOrderPartDto>();
        CreateMap<CreateWorkOrderPartDto, WorkOrderPart>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.QuantityUsed, opt => opt.Ignore())
            .ForMember(dest => dest.TotalCost, opt => opt.MapFrom(src => src.UnitCost * src.QuantityRequired))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => "Required"))
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrder, opt => opt.Ignore());

        CreateMap<UpdateWorkOrderPartDto, WorkOrderPart>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrderId, opt => opt.Ignore())
            .ForMember(dest => dest.TotalCost, opt => opt.MapFrom(src => src.UnitCost * src.QuantityUsed))
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrder, opt => opt.Ignore());

        CreateMap<WorkOrderLabor, WorkOrderLaborDto>()
            .ForMember(dest => dest.Technician, opt => opt.MapFrom(src => src.Technician));

        CreateMap<CreateWorkOrderLaborDto, WorkOrderLabor>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.EndTime, opt => opt.Ignore())
            .ForMember(dest => dest.Hours, opt => opt.Ignore())
            .ForMember(dest => dest.TotalCost, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrder, opt => opt.Ignore())
            .ForMember(dest => dest.Technician, opt => opt.Ignore());

        CreateMap<UpdateWorkOrderLaborDto, WorkOrderLabor>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrderId, opt => opt.Ignore())
            .ForMember(dest => dest.TechnicianId, opt => opt.Ignore())
            .ForMember(dest => dest.TotalCost, opt => opt.MapFrom(src => (decimal)src.Hours * src.HourlyRate))
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrder, opt => opt.Ignore())
            .ForMember(dest => dest.Technician, opt => opt.Ignore());

        // CreateMap<WorkOrderDocument, WorkOrderDocumentDto>()
        //     .ForMember(dest => dest.UploadedBy, opt => opt.MapFrom(src => src.UploadedBy));

        CreateMap<WorkOrderComment, WorkOrderCommentDto>()
            .ForMember(dest => dest.Employee, opt => opt.MapFrom(src => src.User));

        CreateMap<CreateWorkOrderCommentDto, WorkOrderComment>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.EmployeeId, opt => opt.Ignore()) // Set from current user service
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrder, opt => opt.Ignore())
            .ForMember(dest => dest.User, opt => opt.Ignore());

        // Maintenance Scheduling Mappings
        CreateMap<MaintenanceSchedule, MaintenanceScheduleDto>()
            .ForMember(dest => dest.Asset, opt => opt.MapFrom(src => src.Asset))
            .ForMember(dest => dest.MaintenanceType, opt => opt.MapFrom(src => src.MaintenanceType))
            .ForMember(dest => dest.DefaultTechnician, opt => opt.MapFrom(src => src.DefaultTechnician))
            .ForMember(dest => dest.DefaultTeam, opt => opt.MapFrom(src => src.DefaultTeam))
            .ForMember(dest => dest.PriorityLevel, opt => opt.MapFrom(src => src.PriorityLevel));

        CreateMap<CreateMaintenanceScheduleDto, MaintenanceSchedule>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.LastGeneratedDate, opt => opt.Ignore())
            .ForMember(dest => dest.NextDueDate, opt => opt.Ignore()) // Calculate based on schedule type and frequency
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Asset, opt => opt.Ignore())
            .ForMember(dest => dest.MaintenanceType, opt => opt.Ignore())
            .ForMember(dest => dest.DefaultTechnician, opt => opt.Ignore())
            .ForMember(dest => dest.DefaultTeam, opt => opt.Ignore())
            .ForMember(dest => dest.PriorityLevel, opt => opt.Ignore())
            .ForMember(dest => dest.GeneratedWorkOrders, opt => opt.Ignore());

        CreateMap<UpdateMaintenanceScheduleDto, MaintenanceSchedule>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.AssetId, opt => opt.Ignore())
            .ForMember(dest => dest.StartDate, opt => opt.Ignore())
            .ForMember(dest => dest.LastGeneratedDate, opt => opt.Ignore())
            .ForMember(dest => dest.NextDueDate, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Asset, opt => opt.Ignore())
            .ForMember(dest => dest.MaintenanceType, opt => opt.Ignore())
            .ForMember(dest => dest.DefaultTechnician, opt => opt.Ignore())
            .ForMember(dest => dest.DefaultTeam, opt => opt.Ignore())
            .ForMember(dest => dest.PriorityLevel, opt => opt.Ignore())
            .ForMember(dest => dest.GeneratedWorkOrders, opt => opt.Ignore());

        // Inspection Mappings
        CreateMap<InspectionTemplate, InspectionTemplateDto>();
        CreateMap<CreateInspectionTemplateDto, InspectionTemplate>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Inspections, opt => opt.Ignore());

        CreateMap<UpdateInspectionTemplateDto, InspectionTemplate>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Inspections, opt => opt.Ignore());

        CreateMap<AssetInspection, AssetInspectionDto>()
            .ForMember(dest => dest.Asset, opt => opt.MapFrom(src => src.Asset))
            .ForMember(dest => dest.InspectionTemplate, opt => opt.MapFrom(src => src.InspectionTemplate))
            .ForMember(dest => dest.Inspector, opt => opt.MapFrom(src => src.Inspector));

        CreateMap<CreateAssetInspectionDto, AssetInspection>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => "Scheduled"))
            .ForMember(dest => dest.OverallResult, opt => opt.Ignore())
            .ForMember(dest => dest.InspectionData, opt => opt.MapFrom(src => "{}"))
            .ForMember(dest => dest.RecommendedActions, opt => opt.Ignore())
            .ForMember(dest => dest.NextInspectionDue, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Asset, opt => opt.Ignore())
            .ForMember(dest => dest.InspectionTemplate, opt => opt.Ignore())
            .ForMember(dest => dest.Inspector, opt => opt.Ignore())
            .ForMember(dest => dest.Documents, opt => opt.Ignore());

        CreateMap<UpdateAssetInspectionDto, AssetInspection>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.AssetId, opt => opt.Ignore())
            .ForMember(dest => dest.InspectionTemplateId, opt => opt.Ignore())
            .ForMember(dest => dest.Status, opt => opt.Ignore())
            .ForMember(dest => dest.OverallResult, opt => opt.Ignore())
            .ForMember(dest => dest.InspectionData, opt => opt.Ignore())
            .ForMember(dest => dest.RecommendedActions, opt => opt.Ignore())
            .ForMember(dest => dest.NextInspectionDue, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Asset, opt => opt.Ignore())
            .ForMember(dest => dest.InspectionTemplate, opt => opt.Ignore())
            .ForMember(dest => dest.Inspector, opt => opt.Ignore())
            .ForMember(dest => dest.Documents, opt => opt.Ignore());

        // Resource Management Mappings
        CreateMap<TechnicianTeam, TechnicianTeamDto>()
            .ForMember(dest => dest.TeamLeader, opt => opt.MapFrom(src => src.TeamLeader))
            .ForMember(dest => dest.Members, opt => opt.MapFrom(src => src.Members))
            .ForMember(dest => dest.TotalMembers, opt => opt.MapFrom(src => src.Members.Count))
            .ForMember(dest => dest.ActiveMembers, opt => opt.MapFrom(src => src.Members.Count(m => m.IsActive)));

        CreateMap<CreateTechnicianTeamDto, TechnicianTeam>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.TeamLeader, opt => opt.Ignore())
            .ForMember(dest => dest.Members, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrders, opt => opt.Ignore());

        CreateMap<UpdateTechnicianTeamDto, TechnicianTeam>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.TeamLeader, opt => opt.Ignore())
            .ForMember(dest => dest.Members, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrders, opt => opt.Ignore());

        CreateMap<TechnicianTeamMember, TechnicianTeamMemberDto>()
            .ForMember(dest => dest.Team, opt => opt.MapFrom(src => src.Team))
            .ForMember(dest => dest.Technician, opt => opt.MapFrom(src => src.Technician));

        CreateMap<CreateTechnicianTeamMemberDto, TechnicianTeamMember>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.LeftDate, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Team, opt => opt.Ignore())
            .ForMember(dest => dest.Technician, opt => opt.Ignore());

        CreateMap<UpdateTechnicianTeamMemberDto, TechnicianTeamMember>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.TeamId, opt => opt.Ignore())
            .ForMember(dest => dest.TechnicianId, opt => opt.Ignore())
            .ForMember(dest => dest.JoinedDate, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Team, opt => opt.Ignore())
            .ForMember(dest => dest.Technician, opt => opt.Ignore());

        CreateMap<TechnicianSkill, TechnicianSkillDto>()
            .ForMember(dest => dest.UserCount, opt => opt.MapFrom(src => src.UserSkills.Count));

        CreateMap<CreateTechnicianSkillDto, TechnicianSkill>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UserSkills, opt => opt.Ignore());

        CreateMap<UpdateTechnicianSkillDto, TechnicianSkill>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UserSkills, opt => opt.Ignore());

        CreateMap<UserTechnicianSkill, UserTechnicianSkillDto>()
            .ForMember(dest => dest.Employee, opt => opt.MapFrom(src => src.Employee))
            .ForMember(dest => dest.Skill, opt => opt.MapFrom(src => src.Skill));

        CreateMap<CreateUserTechnicianSkillDto, UserTechnicianSkill>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Employee, opt => opt.Ignore())
            .ForMember(dest => dest.Skill, opt => opt.Ignore());

        CreateMap<UpdateUserTechnicianSkillDto, UserTechnicianSkill>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.EmployeeId, opt => opt.Ignore())
            .ForMember(dest => dest.SkillId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Employee, opt => opt.Ignore())
            .ForMember(dest => dest.Skill, opt => opt.Ignore());

        // Asset Downtime Mappings
        CreateMap<AssetDowntime, AssetDowntimeDto>()
            .ForMember(dest => dest.Asset, opt => opt.MapFrom(src => src.Asset))
            .ForMember(dest => dest.WorkOrder, opt => opt.MapFrom(src => src.WorkOrder));

        CreateMap<CreateAssetDowntimeDto, AssetDowntime>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.EndTime, opt => opt.Ignore())
            .ForMember(dest => dest.DowntimeHours, opt => opt.Ignore())
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => "Active"))
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Asset, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrder, opt => opt.Ignore());

        CreateMap<UpdateAssetDowntimeDto, AssetDowntime>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.AssetId, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrderId, opt => opt.Ignore())
            .ForMember(dest => dest.DowntimeHours, opt => opt.MapFrom((src, dest) => 
                src.EndTime.HasValue ? (src.EndTime.Value - src.StartTime).TotalHours : dest.DowntimeHours))
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Asset, opt => opt.Ignore())
            .ForMember(dest => dest.WorkOrder, opt => opt.Ignore());

        // ApplicationUser mapping for DTOs
        CreateMap<ApplicationUser, ApplicationUserDto>();
    }
}