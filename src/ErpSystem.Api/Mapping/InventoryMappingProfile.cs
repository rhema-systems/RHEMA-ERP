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
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : string.Empty));

        CreateMap<InventoryItem, InventoryItemDetailDto>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : string.Empty))
            .ForMember(dest => dest.Locations, opt => opt.MapFrom(src => src.InventoryLocations))
            .ForMember(dest => dest.RecentMovements, opt => opt.Ignore());

        CreateMap<CreateInventoryItemDto, InventoryItem>();

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
