using AutoMapper;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Api.Mapping
{
    public class HRMappingProfile : Profile
    {
        public HRMappingProfile()
        {
            // Employee mappings
            CreateMap<Employee, EmployeeDto>()
                .ForMember(dest => dest.DepartmentName, opt => opt.MapFrom(src => src.Department.Name))
                .ForMember(dest => dest.PositionTitle, opt => opt.MapFrom(src => src.Position.Title))
                .ForMember(dest => dest.SectionName, opt => opt.MapFrom(src => src.Section != null ? src.Section.Name : null))
                .ForMember(dest => dest.LocationName, opt => opt.MapFrom(src => src.Location != null ? src.Location.Name : null));

            CreateMap<Employee, EmployeeDetailDto>()
                .ForMember(dest => dest.DepartmentName, opt => opt.MapFrom(src => src.Department.Name))
                .ForMember(dest => dest.PositionTitle, opt => opt.MapFrom(src => src.Position.Title))
                .ForMember(dest => dest.SectionName, opt => opt.MapFrom(src => src.Section != null ? src.Section.Name : null))
                // [HR-MODULE-PORT] Employee.Shift removed: the HR port replaces the old single-shift
                // link with HRApi's richer attendance model (ShiftAssignment / EmployeeWorkSchedule).
                .ForMember(dest => dest.CountryName, opt => opt.MapFrom(src => src.Country != null ? src.Country.Name : null));

            CreateMap<CreateEmployeeDto, Employee>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedBy, opt => opt.Ignore())
                // ⚠ The geography link is deliberately NOT mapped here. These two profiles are used
                // by the LEGACY api/Employees controller, which writes straight to the entity and
                // never reaches EmployeeService — so nothing on that path would rewrite State and
                // City from the tree, and the record would end up carrying an area that disagrees
                // with its own printed address. The whole point of the module is that those two
                // cannot diverge. GeoAreaId is settable only through api/hr/Employees, which goes
                // through the service. See docs/GEOGRAPHY-REFERENCE-DESIGN.md §6.4.
                .ForMember(dest => dest.GeoAreaId, opt => opt.Ignore())
                .ForMember(dest => dest.TenantId, opt => opt.Ignore());

            CreateMap<UpdateEmployeeDto, Employee>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                // ⚠ See the create profile above — same reason.
                .ForMember(dest => dest.GeoAreaId, opt => opt.Ignore())
                .ForMember(dest => dest.TenantId, opt => opt.Ignore());

            // Department mappings
            CreateMap<Department, DepartmentDto>();
            CreateMap<CreateDepartmentDto, Department>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.TenantId, opt => opt.Ignore());

            // EmployeePosition mappings
            CreateMap<EmployeePosition, EmployeePositionDto>()
                // [HR-MODULE-PORT] EmployeePosition.Department removed: positions now anchor to the org
                // structure via OrganizationUnit / OrganizationLevel instead of a department FK.
                .ForMember(dest => dest.OrganizationLevelName, opt => opt.MapFrom(src => src.OrganizationLevel != null ? src.OrganizationLevel.Name : null))
                .ForMember(dest => dest.OrganizationUnitName, opt => opt.MapFrom(src => src.OrganizationUnit != null ? src.OrganizationUnit.Name : null));

            CreateMap<CreateEmployeePositionDto, EmployeePosition>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.TenantId, opt => opt.Ignore());

            // Section mappings
            CreateMap<Section, SectionDto>()
                .ForMember(dest => dest.DepartmentName, opt => opt.MapFrom(src => src.Department.Name));
        }
    }
}
