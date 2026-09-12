using AutoMapper;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;

namespace ErpSystem.Api.Mapping;

public class InventoryMappingProfile : Profile
{
    public InventoryMappingProfile()
    {
        // InventoryItem mappings
        CreateMap<InventoryItem, InventoryItemDto>()
            .ForMember(dest => dest.AverageCost, opt => opt.MapFrom(src => src.AverageCost))
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : string.Empty))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.Status == ErpSystem.Core.Enums.ItemStatus.Active))
            .ForMember(dest => dest.UnitOfMeasureScheduleId, opt => opt.MapFrom(src => src.UnitOfMeasureScheduleId))
            .ForMember(dest => dest.UnitOfMeasureScheduleName, opt => opt.MapFrom(src => src.UnitOfMeasureSchedule != null ? src.UnitOfMeasureSchedule.Description : null))
            .ForMember(dest => dest.ValuationMethod, opt => opt.MapFrom(src => src.ValuationMethod))
            .ForMember(dest => dest.IsValuationLocked, opt => opt.MapFrom(src => src.IsValuationLocked))
            .ForMember(dest => dest.MinimumShelfLifeDays, opt => opt.MapFrom(src => src.ShelfLifeDays ?? 0))
            .ForMember(dest => dest.RowVersion, opt => opt.MapFrom(src => Convert.ToBase64String(src.RowVersion)));

        CreateMap<InventoryItem, InventoryItemDetailDto>()
            .ForMember(dest => dest.AverageCost, opt => opt.MapFrom(src => src.AverageCost))
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : string.Empty))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.Status == ErpSystem.Core.Enums.ItemStatus.Active))
            .ForMember(dest => dest.UnitOfMeasureScheduleId, opt => opt.MapFrom(src => src.UnitOfMeasureScheduleId))
            .ForMember(dest => dest.UnitOfMeasureScheduleName, opt => opt.MapFrom(src => src.UnitOfMeasureSchedule != null ? src.UnitOfMeasureSchedule.Description : null))
            .ForMember(dest => dest.RowVersion, opt => opt.MapFrom(src => Convert.ToBase64String(src.RowVersion)))
            .ForMember(dest => dest.MinimumShelfLifeDays, opt => opt.MapFrom(src => src.ShelfLifeDays ?? 0))
            .ForMember(dest => dest.Locations, opt => opt.MapFrom(src => src.InventoryLocations))
            .ForMember(dest => dest.RecentMovements, opt => opt.Ignore());

        CreateMap<CreateInventoryItemDto, InventoryItem>()
            .ForMember(dest => dest.ShelfLifeDays, opt => opt.MapFrom(src => src.MinimumShelfLifeDays));
        
        CreateMap<UpdateInventoryItemDto, InventoryItem>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.TenantId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
            .ForMember(dest => dest.RowVersion, opt => opt.Ignore())
            .ForMember(dest => dest.ShelfLifeDays, opt => opt.MapFrom(src => src.MinimumShelfLifeDays))
            .ForMember(dest => dest.Status, opt => opt.Ignore()); // Handled separately in controller

        // InventoryLocation mappings
        CreateMap<InventoryLocation, InventoryLocationDto>()
            .ForMember(dest => dest.LocationName, opt => opt.Ignore())
            .ForMember(dest => dest.WarehouseName, opt => opt.Ignore())
            .ForMember(dest => dest.AvailableQuantity, opt => opt.Ignore())
            .ForMember(dest => dest.AllocatedQuantity, opt => opt.Ignore());

        // StockMovement mappings
        CreateMap<StockMovement, StockMovementDto>()
            .ForMember(dest => dest.ProcessedBy, opt => opt.Ignore());

        CreateMap<CreateStockMovementDto, StockMovement>();

        // InventoryAllocation mappings
        CreateMap<InventoryAllocation, InventoryAllocationDto>()
            .ForMember(dest => dest.ItemCode, opt => opt.MapFrom(src => src.InventoryItem != null ? src.InventoryItem.ItemCode : string.Empty))
            .ForMember(dest => dest.ItemName, opt => opt.MapFrom(src => src.InventoryItem != null ? src.InventoryItem.Name : string.Empty))
            .ForMember(dest => dest.LocationCode, opt => opt.MapFrom(src => src.Location != null ? src.Location.LocationCode : string.Empty));

        CreateMap<AllocateInventoryDto, InventoryAllocation>();

        // Warehouse mappings
        CreateMap<Warehouse, WarehouseDto>();
        CreateMap<WarehouseLocation, WarehouseLocationDto>();
    }
}
