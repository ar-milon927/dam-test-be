using AutoMapper;
using NinjaDAM.Entity.Entities;
using NinjaDAM.DTO.Company;
using NinjaDAM.DTO.Register;
using NinjaDAM.DTO.SuperAdmin;
using NinjaDAM.DTO.User;
using NinjaDAM.DTO.UserManagement;
using NinjaDAM.DTO.Folder;
using NinjaDAM.DTO.Asset;
using NinjaDAM.DTO.AssetCollection;
using NinjaDAM.DTO.VisualTag;
using NinjaDAM.DTO.MetadataField;
using NinjaDAM.DTO.Permission;
using NinjaDAM.DTO.Group;
using NinjaDAM.DTO.CollectionShare;
using NinjaDAM.DTO.AssetShare;

namespace NinjaDAM.Services.Mapping
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // RegisterDto -> Users
            CreateMap<RegisterDto, Users>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.Email))
                .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.IsApproved, opt => opt.Ignore())
                .ForMember(dest => dest.IsFirstLogin, opt => opt.Ignore())
                .ForMember(dest => dest.CompanyId, opt => opt.Ignore());

            // Users -> UserDto
            CreateMap<Users, UserDto>()
                .ForMember(dest => dest.Token, opt => opt.Ignore())
                .ForMember(dest => dest.Role, opt => opt.Ignore())
                .ForMember(dest => dest.CompanyName, opt => opt.Ignore());

            // RegisterDto -> Company (when creating a new company)
            CreateMap<RegisterDto, Company>()
                    .ForMember(dest => dest.Id, opt => opt.Ignore())
                    .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                    .ForMember(dest => dest.StorageTier, opt => opt.MapFrom(src =>
                        string.IsNullOrWhiteSpace(src.StorageTier) || src.StorageTier.Trim().ToLower() == "string" ? null : src.StorageTier.Trim() ));


            CreateMap<Users, PendingUserDto>()
                .ForMember(dest => dest.CompanyName, opt => opt.MapFrom(src => src.Company != null ? src.Company.CompanyName : null))
                .ForMember(dest => dest.Role, opt => opt.Ignore());

            CreateMap<Company, CompanyDto>().ReverseMap();

            // Folder mappings
            CreateMap<Folder, FolderDto>()
                .ForMember(dest => dest.Count, opt => opt.MapFrom(src => src.AssetCount));

            CreateMap<CreateFolderDto, Folder>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.CompanyId, opt => opt.Ignore())
                .ForMember(dest => dest.AssetCount, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());

            // Asset mappings
            CreateMap<Asset, AssetDto>()
                .ForMember(dest => dest.FilePath, opt => opt.MapFrom(src => 
                    GetRelativePath(src.FilePath)))
                .ForMember(dest => dest.ThumbnailPath, opt => opt.MapFrom(src => 
                    !string.IsNullOrEmpty(src.ThumbnailPath) ? GetRelativePath(src.ThumbnailPath) : null))
                .ForMember(dest => dest.UserMetadata, opt => opt.MapFrom(src => src.UserMetadata))
                .ForMember(dest => dest.IptcMetadata, opt => opt.MapFrom(src => src.IptcMetadata));

            // Collection mappings
            CreateMap<Collection, CollectionDto>();
            
            CreateMap<Collection, CollectionWithAssetsDto>()
                .ForMember(dest => dest.Assets, opt => opt.Ignore()); // Manually mapped in service
            
            CreateMap<CreateCollectionDto, Collection>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.CompanyId, opt => opt.Ignore())
                .ForMember(dest => dest.AssetCount, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());

            // VisualTag mappings
            CreateMap<VisualTag, VisualTagDto>();
            
            CreateMap<CreateVisualTagDto, VisualTag>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.CompanyId, opt => opt.Ignore())
                .ForMember(dest => dest.AssetCount, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());

            CreateMap<MetadataField, MetadataFieldDto>();

            CreateMap<ControlledVocabularyValue, ControlledVocabularyValueDto>();

            CreateMap<Users, UserResponseDto>()
                .ForMember(dest => dest.Role, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore());

            // Permission mappings
            CreateMap<PermissionDetail, PermissionDetailDto>().ReverseMap();
            CreateMap<CreatePermissionDetailDto, PermissionDetail>();

            // Group mappings
            CreateMap<Group, GroupDto>().ReverseMap();
            CreateMap<CreateGroupDto, Group>();
            CreateMap<UpdateGroupDto, Group>();

            // Collection Share mappings
            CreateMap<CollectionShareLink, CollectionShareLinkDto>()
                .ForMember(dest => dest.ShareUrl, opt => opt.Ignore()); // Set in service
            CreateMap<Asset, SharedAssetDto>()
                .ForMember(dest => dest.ThumbnailPath, opt => opt.MapFrom(src => GetRelativePath(src.ThumbnailPath)))
                .ForMember(dest => dest.FilePath, opt => opt.MapFrom(src => GetRelativePath(src.FilePath)));

            // Asset Share mappings
            CreateMap<AssetShareLink, AssetShareLinkDto>()
                .ForMember(dest => dest.ShareUrl, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.TimeRemaining, opt => opt.Ignore()); // Set in service
        }
        
        private static string GetRelativePath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return fullPath;
            
            // Convert to forward slashes
            fullPath = fullPath.Replace("\\", "/");
            
            // Extract path after wwwroot
            var wwwrootIndex = fullPath.IndexOf("wwwroot/");
            if (wwwrootIndex >= 0)
            {
                return "/" + fullPath.Substring(wwwrootIndex + 8); // Skip "wwwroot/"
            }
            
            // If wwwroot not found, try to find /uploads/ directly
            var uploadsIndex = fullPath.IndexOf("/uploads/");
            if (uploadsIndex >= 0)
            {
                return fullPath.Substring(uploadsIndex);
            }
            
            return fullPath;
        }
    }
}
