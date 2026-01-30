using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NinjaDAM.Entity.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace NinjaDAM.Entity.Data
{
    public class AppDbContext : IdentityDbContext<Users>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options): base(options){ }


        public DbSet<Company> Companies { get; set; }
        public DbSet<VerifyEmail> EmailVerification { get; set; }
        public DbSet<UserPasswordHistory> UserPasswordHistories { get; set; }
        public DbSet<Folder> Folders { get; set; }
        public DbSet<Asset> Assets { get; set; }
        public DbSet<Collection> Collections { get; set; }
        public DbSet<CollectionAsset> CollectionAssets { get; set; }
        public DbSet<VisualTag> VisualTags { get; set; }
        public DbSet<AssetTag> AssetTags { get; set; }
        public DbSet<MetadataField> MetadataFields { get; set; }
        public DbSet<ControlledVocabularyValue> ControlledVocabularyValues { get; set; }
        public DbSet<PermissionDetail> PermissionDetails { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<UserGroup> UserGroups { get; set; }
        public DbSet<GroupPermission> GroupPermissions { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<CollectionShareLink> CollectionShareLinks { get; set; }
        public DbSet<AssetShareLink> AssetShareLinks { get; set; }
        public DbSet<ShareLinkAuditLog> ShareLinkAuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Users>() 
                   .HasOne(u => u.Company)
                   .WithMany(c => c.Users)
                   .HasForeignKey(u => u.CompanyId)
                   .OnDelete(DeleteBehavior.Restrict);

            // Folder relationships
            builder.Entity<Folder>()
                   .HasOne(f => f.ParentFolder)
                   .WithMany(f => f.SubFolders)
                   .HasForeignKey(f => f.ParentId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Folder>()
                   .HasOne(f => f.User)
                   .WithMany()
                   .HasForeignKey(f => f.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Asset relationships
            builder.Entity<Asset>()
                   .HasOne(a => a.Folder)
                   .WithMany(f => f.Assets)
                   .HasForeignKey(a => a.FolderId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Asset>()
                   .HasOne(a => a.User)
                   .WithMany()
                   .HasForeignKey(a => a.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Collection relationships
            builder.Entity<Collection>()
                   .HasOne(c => c.User)
                   .WithMany()
                   .HasForeignKey(c => c.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            // CollectionAsset relationships
            builder.Entity<CollectionAsset>()
                   .HasOne(ca => ca.Collection)
                   .WithMany(c => c.CollectionAssets)
                   .HasForeignKey(ca => ca.CollectionId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<CollectionAsset>()
                   .HasOne(ca => ca.Asset)
                   .WithMany()
                   .HasForeignKey(ca => ca.AssetId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Unique constraint: An asset can only be added once to a collection
            builder.Entity<CollectionAsset>()
                   .HasIndex(ca => new { ca.CollectionId, ca.AssetId })
                   .IsUnique();

            // VisualTag relationships
            builder.Entity<VisualTag>()
                   .HasOne(vt => vt.User)
                   .WithMany()
                   .HasForeignKey(vt => vt.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            // AssetTag relationships
            builder.Entity<Asset>()
                   .HasMany(a => a.AssetTags)
                   .WithOne(at => at.Asset)
                   .HasForeignKey(at => at.AssetId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AssetTag>()
                   .HasOne(at => at.VisualTag)
                   .WithMany(vt => vt.AssetTags)
                   .HasForeignKey(at => at.VisualTagId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Unique constraint: An asset can only have a specific tag once
            builder.Entity<AssetTag>()
                   .HasIndex(at => new { at.AssetId, at.VisualTagId })
                   .IsUnique();

            builder.Entity<ControlledVocabularyValue>()
                   .HasOne(cvv => cvv.MetadataField)
                   .WithMany()
                   .HasForeignKey(cvv => cvv.MetadataFieldId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ControlledVocabularyValue>()
                   .HasOne(cvv => cvv.User)
                   .WithMany()
                   .HasForeignKey(cvv => cvv.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ControlledVocabularyValue>()
                   .HasIndex(cvv => new { cvv.MetadataFieldId, cvv.Value })
                   .IsUnique();

            // Permission relationships
            builder.Entity<UserPermission>()
                   .HasKey(up => new { up.UserId, up.PermissionDetailId });

            builder.Entity<UserPermission>()
                   .HasOne(up => up.User)
                   .WithMany(u => u.UserPermissions)
                   .HasForeignKey(up => up.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserPermission>()
                   .HasOne(up => up.PermissionDetail)
                   .WithMany(pd => pd.UserPermissions)
                   .HasForeignKey(up => up.PermissionDetailId)
                   .OnDelete(DeleteBehavior.Cascade);

            // RolePermission relationships
            builder.Entity<RolePermission>()
                   .HasKey(rp => new { rp.RoleId, rp.PermissionDetailId });

            builder.Entity<RolePermission>()
                    .HasOne(rp => rp.Role)
                    .WithMany()
                    .HasForeignKey(rp => rp.RoleId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<RolePermission>()
                    .HasOne(rp => rp.PermissionDetail)
                    .WithMany()
                    .HasForeignKey(rp => rp.PermissionDetailId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Group relationships
            builder.Entity<Group>()
                   .HasOne(g => g.Company)
                   .WithMany()
                   .HasForeignKey(g => g.CompanyId)
                   .OnDelete(DeleteBehavior.Cascade);

            // UserGroup relationships
            builder.Entity<UserGroup>()
                   .HasKey(ug => new { ug.UserId, ug.GroupId });

            builder.Entity<UserGroup>()
                   .HasOne(ug => ug.User)
                   .WithMany(u => u.UserGroups)
                   .HasForeignKey(ug => ug.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserGroup>()
                   .HasOne(ug => ug.Group)
                   .WithMany()
                   .HasForeignKey(ug => ug.GroupId)
                   .OnDelete(DeleteBehavior.Cascade);

            // GroupPermission relationships
            builder.Entity<GroupPermission>()
                   .HasKey(gp => new { gp.GroupId, gp.PermissionDetailId });

            builder.Entity<GroupPermission>()
                   .HasOne(gp => gp.Group)
                   .WithMany()
                   .HasForeignKey(gp => gp.GroupId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<GroupPermission>()
                   .HasOne(gp => gp.PermissionDetail)
                   .WithMany()
                   .HasForeignKey(gp => gp.PermissionDetailId)
                   .OnDelete(DeleteBehavior.Cascade);

            // CartItem relationships
            builder.Entity<CartItem>()
                   .HasOne(ci => ci.Asset)
                   .WithMany()
                   .HasForeignKey(ci => ci.AssetId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Unique constraint: An asset can only be in a user's cart once
            builder.Entity<CartItem>()
                   .HasIndex(ci => new { ci.UserId, ci.AssetId })
                   .IsUnique();

            // CollectionShareLink relationships
            builder.Entity<CollectionShareLink>()
                   .HasOne(csl => csl.Collection)
                   .WithMany()
                   .HasForeignKey(csl => csl.CollectionId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Index on Token for fast lookups
            builder.Entity<CollectionShareLink>()
                   .HasIndex(csl => csl.Token)
                   .IsUnique();

            // Index on ExpiresAt for cleanup queries
            builder.Entity<CollectionShareLink>()
                   .HasIndex(csl => csl.ExpiresAt);

            // AssetShareLink relationships and indexes
            builder.Entity<AssetShareLink>()
                   .HasOne(asl => asl.Asset)
                   .WithMany()
                   .HasForeignKey(asl => asl.AssetId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Index on Token for fast lookups
            builder.Entity<AssetShareLink>()
                   .HasIndex(asl => asl.Token)
                   .IsUnique();

            // Index on ExpiresAt for cleanup queries
            builder.Entity<AssetShareLink>()
                   .HasIndex(asl => asl.ExpiresAt);
        }
    }
}










