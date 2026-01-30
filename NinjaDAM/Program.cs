using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NinjaDAM.DAL.Repositories;
using NinjaDAM.Entity.Data;
using NinjaDAM.Entity.Entities;
using NinjaDAM.Entity.IRepositories;
using NinjaDAM.Entity.Repositories;
using NinjaDAM.Services.IServices;
using NinjaDAM.Services.Logging;
using NinjaDAM.Services.Mapping;
using NinjaDAM.Services.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = null;
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
});

builder.Logging.AddProvider(new FileLoggerProvider(
    Path.Combine(builder.Environment.WebRootPath ?? "wwwroot", "Errorlogs")
));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 32))
    )
    .LogTo(Console.WriteLine, LogLevel.Information)
    .EnableSensitiveDataLogging()
    .EnableDetailedErrors()
);

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<ICollectionShareLinkRepository, CollectionShareLinkRepository>();
builder.Services.AddScoped<IAssetShareLinkRepository, AssetShareLinkRepository>();
builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<IRegisterService, RegisterService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IResetPasswordService, ResetPasswordService>();
builder.Services.AddScoped<IForgotPasswordService, ForgotPasswordService>();
builder.Services.AddScoped<ISuperAdminService, SuperAdminService>();
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services.AddScoped<IFolderService, FolderService>();
builder.Services.AddScoped<IThumbnailService, ThumbnailService>();
builder.Services.AddScoped<IIptcExtractionService, IptcExtractionService>();
builder.Services.AddScoped<IAssetService, AssetService>();
builder.Services.AddScoped<ICollectionService, CollectionService>();
builder.Services.AddScoped<IVisualTagService, VisualTagService>();
builder.Services.AddScoped<IMetadataFieldService, MetadataFieldService>();
builder.Services.AddScoped<IControlledVocabularyValueService, ControlledVocabularyValueService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IGroupService, GroupService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<ICollectionShareService, CollectionShareService>();
builder.Services.AddScoped<IAssetShareService, AssetShareService>();
builder.Services.AddScoped<IAdminShareLinkService, AdminShareLinkService>();
builder.Services.AddHostedService<RecycleBinCleanupService>();

builder.Services.AddAutoMapper(typeof(MappingProfile));

builder.Services.AddIdentity<Users, IdentityRole>(options =>
{
    // Disable automatic redirect to login page for API
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Configure cookie policy to not redirect on 401
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "DefaultSecretKeyPlaceholder_MustBeChangedInProduction")
        )
    };
    
    // Don't redirect on auth failure, return 401
    options.Events = new JwtBearerEvents
    {
        OnChallenge = context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsync("{\"message\":\"Unauthorized\"}");
        }
    };
});

builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = true;
    });

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = long.MaxValue;
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBoundaryLengthLimit = int.MaxValue;
    options.MultipartHeadersCountLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.WithOrigins(
                       "http://localhost:5173", 
                       "http://localhost:3000",
                       "https://nijnadam-fe.vercel.app",
                       "https://dam.webatlas.tech",
                       "https://qa2dam.webatlas.tech",
                       "https://qa3dam.webatlas.tech",
                       "https://damapiqa2.webatlastech.com")
                   .AllowAnyHeader()
                   .AllowAnyMethod()
                   .AllowCredentials();
        });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Ninja DAM API", Version = "v1.0" });

    // Adding security scheme
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter JWT token like: **Bearer {token}**",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });

    // Adding global security requirement
    c.AddSecurityRequirement(new OpenApiSecurityRequirement {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Auto-migrate database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<AppDbContext>();
    
    // Apply pending migrations
    await dbContext.Database.MigrateAsync();

    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<Users>>();

    var companyRepo = services.GetRequiredService<IRepository<Company>>();
    var assetRepo = services.GetRequiredService<IRepository<Asset>>();
    var folderRepo = services.GetRequiredService<IRepository<Folder>>();

    await SeedRolesAsync(roleManager);
    await SeedSuperAdminAsync(userManager, companyRepo, assetRepo, folderRepo);
    
    var permissionRepo = services.GetRequiredService<IRepository<PermissionDetail>>();
    await SeedPermissionsAsync(permissionRepo);
}


async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
{
    string[] roleNames = { "SuperAdmin", "Admin", "Editor", "Viewer", "Manager", "User" };
    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            var result = await roleManager.CreateAsync(new IdentityRole(roleName));
            if (result.Succeeded)
                Console.WriteLine($"Role '{roleName}' created.");
            else
                Console.WriteLine($"Failed to create role '{roleName}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }
}


async Task SeedSuperAdminAsync(UserManager<Users> userManager, IRepository<Company> companyRepo, IRepository<Asset> assetRepo, IRepository<Folder> folderRepo)
{
    string superAdminEmail = "akashthakur37442@gmail.com";

    // Ensure default company exists
    var defaultCompany = await companyRepo.GetSingleAsync(c => c.CompanyName == "NinjaDAM");
    if (defaultCompany == null)
    {
        defaultCompany = new Company
        {
            Id = Guid.NewGuid(),
            CompanyName = "NinjaDAM",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        await companyRepo.AddAsync(defaultCompany);
        await companyRepo.SaveAsync();
        Console.WriteLine("Default Company 'NinjaDAM' created.");
    }

    var superAdmin = await userManager.FindByEmailAsync(superAdminEmail);
    if (superAdmin != null) 
    {
        // Update existing SuperAdmin with CompanyId if missing
        if (superAdmin.CompanyId == null)
        {
            superAdmin.CompanyId = defaultCompany.Id;
            await userManager.UpdateAsync(superAdmin);
            Console.WriteLine("Updated existing SuperAdmin with Default Company ID.");
        }
    }
    else
    {
        superAdmin = new Users
        {
            FirstName = "Akash",
            LastName = "kumar",
            UserName = "superadmin",
            Email = superAdminEmail,
            IsActive = true,
            IsApproved = true,
            IsFirstLogin = false,
            CompanyId = defaultCompany.Id // Assign default company
        };

        string superAdminPassword = "SuperAdmin@123"; // strong default password
        var result = await userManager.CreateAsync(superAdmin, superAdminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(superAdmin, "SuperAdmin");
            Console.WriteLine("Default SuperAdmin created successfully with Company ID.");
        }
        else
        {
            Console.WriteLine($"Failed to create SuperAdmin: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    // MIGRATION: Move orphan assets/folders (CompanyId = null) to the default company
    // This ensures files uploaded before the company was created are visible to the SuperAdmin.
    
    var orphanAssets = await assetRepo.Query().Where(a => a.CompanyId == null).ToListAsync();
    if (orphanAssets.Any())
    {
        foreach (var asset in orphanAssets)
        {
            asset.CompanyId = defaultCompany.Id;
            assetRepo.Update(asset);
        }
        await assetRepo.SaveAsync();
        Console.WriteLine($"Migrated {orphanAssets.Count} orphan assets to Default Company.");
    }

    var orphanFolders = await folderRepo.Query().Where(f => f.CompanyId == null).ToListAsync();
    if (orphanFolders.Any())
    {
        foreach (var folder in orphanFolders)
        {
            folder.CompanyId = defaultCompany.Id;
            folderRepo.Update(folder);
        }
        await folderRepo.SaveAsync();
        Console.WriteLine($"Migrated {orphanFolders.Count} orphan folders to Default Company.");
    }

    // MIGRATION: Move orphan USERS to default company
    // This fixes the Admin user created before the UserManagementService fix.
    var orphanUsers = await userManager.Users.Where(u => u.CompanyId == null && u.Id != superAdmin.Id).ToListAsync();
    if (orphanUsers.Any())
    {
        foreach (var user in orphanUsers)
        {
            user.CompanyId = defaultCompany.Id;
            await userManager.UpdateAsync(user);
        }
        Console.WriteLine($"Migrated {orphanUsers.Count} orphan users to Default Company.");
    }
}

async Task SeedPermissionsAsync(IRepository<PermissionDetail> permissionRepo)
{
    var permissions = new List<(string Name, string Group)>
    {
        // User Management
        ("Create User", "User Management"),
        ("Edit User", "User Management"),
        ("Delete User", "User Management"),
        ("View User", "User Management"),
        ("Edit user roles", "User Management"),
        ("Manage user status", "User Management"),
        ("Create Group", "User Management"),
        ("Edit Group", "User Management"),
        ("Delete Group", "User Management"),
        ("View groups", "User Management"),

        // Asset Management
        ("Upload assets", "Asset Management"),
        ("Edit metadata", "Asset Management"),
        ("Move assets", "Asset Management"),
        ("Delete assets", "Asset Management"),
        ("Share assets", "Asset Management"),
        ("View assets", "Asset Management"),
        ("Create folders", "Asset Management"),
        ("Edit folders", "Asset Management"),
        ("Move folders", "Asset Management"),
        ("Delete folders", "Asset Management"),
        ("View folders", "Asset Management"),

        // Collections
        ("Create collections", "Collections"),
        ("Edit collections", "Collections"),
        ("Share collections", "Collections"),
        ("Delete collections", "Collections"),
        ("View collections", "Collections"),

        // Configuration
        ("Create metadata fields", "Configuration"),
        ("Manage taxonomy", "Configuration"),
        ("Manage visual tags", "Configuration"),
        ("Configure system", "Configuration"),
        ("View metadata", "Configuration"),
        ("View taxonomy", "Configuration")
    };

    foreach (var (name, group) in permissions)
    {
        var existing = await permissionRepo.Query().FirstOrDefaultAsync(p => p.PermissionName == name);
        if (existing == null)
        {
            await permissionRepo.AddAsync(new PermissionDetail
            {
                Id = Guid.NewGuid(),
                PermissionName = name,
                ByDefault = group,
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            Console.WriteLine($"Seeded Permission: {name}");
        }
        else if (existing.IsDeleted) // Restore if deleted
        {
             existing.IsDeleted = false;
             permissionRepo.Update(existing);
             Console.WriteLine($"Restored Permission: {name}");
        }
    }
    await permissionRepo.SaveAsync();
}


app.UseSwagger();
app.UseSwaggerUI();
app.UseMiddleware<ErrorLoggingMiddleware>();
app.MapGet("/", () => Results.Redirect("/swagger"));

app.UseHttpsRedirection();

// Enable static files (for serving uploaded assets)
app.UseStaticFiles();

app.UseCors("AllowReactApp");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
