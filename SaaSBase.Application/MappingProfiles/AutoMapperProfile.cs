using AutoMapper;
using SaaSBase.Application.DTOs;
using SaaSBase.Domain;

namespace SaaSBase.Application.MappingProfiles;

/// <summary>
/// AutoMapper profile for mapping between domain entities and DTOs
/// </summary>
public class AutoMapperProfile : Profile
{
	public AutoMapperProfile()
	{
		// User mappings
		CreateMap<User, UserDetailsDto>()
			.ReverseMap();

		CreateMap<CreateUserDto, User>()
			.ForMember(dest => dest.Id, opt => opt.Ignore())
			.ForMember(dest => dest.OrganizationId, opt => opt.Ignore())
			.ForMember(dest => dest.CreatedAtUtc, opt => opt.Ignore())
			.ForMember(dest => dest.ModifiedAtUtc, opt => opt.Ignore())
			.ForMember(dest => dest.DeletedAtUtc, opt => opt.Ignore())
			.ForMember(dest => dest.IsDeleted, opt => opt.Ignore());

		CreateMap<UpdateUserDto, User>()
			.ForMember(dest => dest.Id, opt => opt.Ignore())
			.ForMember(dest => dest.OrganizationId, opt => opt.Ignore())
			.ForMember(dest => dest.CreatedAtUtc, opt => opt.Ignore())
			.ForMember(dest => dest.ModifiedAtUtc, opt => opt.Ignore())
			.ForMember(dest => dest.DeletedAtUtc, opt => opt.Ignore())
			.ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
			.ForMember(dest => dest.Email, opt => opt.Ignore())
			.ForMember(dest => dest.PasswordHash, opt => opt.Ignore());

		// Role mappings
		CreateMap<Role, RoleDto>()
			.ReverseMap();

		CreateMap<CreateRoleDto, Role>()
			.ForMember(dest => dest.Id, opt => opt.Ignore())
			.ForMember(dest => dest.OrganizationId, opt => opt.Ignore())
			.ForMember(dest => dest.CreatedAtUtc, opt => opt.Ignore())
			.ForMember(dest => dest.ModifiedAtUtc, opt => opt.Ignore())
			.ForMember(dest => dest.DeletedAtUtc, opt => opt.Ignore())
			.ForMember(dest => dest.IsDeleted, opt => opt.Ignore());

		CreateMap<UpdateRoleDto, Role>()
			.ForMember(dest => dest.Id, opt => opt.Ignore())
			.ForMember(dest => dest.OrganizationId, opt => opt.Ignore())
			.ForMember(dest => dest.CreatedAtUtc, opt => opt.Ignore())
			.ForMember(dest => dest.ModifiedAtUtc, opt => opt.Ignore())
			.ForMember(dest => dest.DeletedAtUtc, opt => opt.Ignore())
			.ForMember(dest => dest.IsDeleted, opt => opt.Ignore());

		// Permission mappings
		CreateMap<Permission, PermissionDto>()
			.ReverseMap();

		CreateMap<CreatePermissionDto, Permission>()
			.ForMember(dest => dest.Id, opt => opt.Ignore())
			.ForMember(dest => dest.OrganizationId, opt => opt.Ignore())
			.ForMember(dest => dest.CreatedAtUtc, opt => opt.Ignore())
			.ForMember(dest => dest.ModifiedAtUtc, opt => opt.Ignore())
			.ForMember(dest => dest.DeletedAtUtc, opt => opt.Ignore())
			.ForMember(dest => dest.IsDeleted, opt => opt.Ignore());

		CreateMap<UpdatePermissionDto, Permission>()
			.ForMember(dest => dest.Id, opt => opt.Ignore())
			.ForMember(dest => dest.OrganizationId, opt => opt.Ignore())
			.ForMember(dest => dest.CreatedAtUtc, opt => opt.Ignore())
			.ForMember(dest => dest.ModifiedAtUtc, opt => opt.Ignore())
			.ForMember(dest => dest.DeletedAtUtc, opt => opt.Ignore())
			.ForMember(dest => dest.IsDeleted, opt => opt.Ignore());

		// Add other entity mappings as needed
		// Product, Category, etc.
	}
}
