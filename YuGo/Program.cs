using YuGo.Data;
using YuGo.Interfaces;
using YuGo.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi;
using YuGo.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer"
    });

    c.OperationFilter<AuthResponsesOperationFilter>();
});

builder.Services.AddSingleton<DbConnectionFactory>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.Configure<YuGo.Helpers.CloudinarySettings>(builder.Configuration.GetSection("CloudinarySettings"));
builder.Services.AddScoped<IPhotoService, PhotoService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IGeminiService, GeminiService>();
builder.Services.AddScoped<ITripService, TripService>();
builder.Services.AddSignalR();
builder.Services.AddHostedService<NotificationBackgroundService>();

// Configure JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // First check the Authorization header
            string? authHeader = context.Request.Headers["Authorization"];
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }

            // Fallback to cookie for Swagger auto-authorization
            var token = context.Request.Cookies["X-Access-Token"];
            if (!string.IsNullOrEmpty(token))
            {
                context.Token = token;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

var app = builder.Build();

app.UseStaticFiles();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

// Initialize Database
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<DbConnectionFactory>();
    DbInitializer.Initialize(factory.ConnectionString);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.InjectJavascript("/swagger-custom.js");
    });
}

app.MapControllers();
app.MapHub<NotificationHub>("/notificationHub");

app.Run();

// Define Operation Filter for Swagger
public class AuthResponsesOperationFilter : Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter
{
    public void Apply(Microsoft.OpenApi.OpenApiOperation operation, Swashbuckle.AspNetCore.SwaggerGen.OperationFilterContext context)
    {
        var hasAuthorize = System.Linq.Enumerable.Any(System.Linq.Enumerable.OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(context.MethodInfo.DeclaringType?.GetCustomAttributes(true) ?? System.Array.Empty<object>())) ||
                           System.Linq.Enumerable.Any(System.Linq.Enumerable.OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(context.MethodInfo.GetCustomAttributes(true)));

        var allowAnonymous = System.Linq.Enumerable.Any(System.Linq.Enumerable.OfType<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>(context.MethodInfo.GetCustomAttributes(true)));

        if (hasAuthorize && !allowAnonymous)
        {
            operation.Security ??= new System.Collections.Generic.List<Microsoft.OpenApi.OpenApiSecurityRequirement>();

            var requirement = new Microsoft.OpenApi.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer"),
                    new System.Collections.Generic.List<string>()
                }
            };
            
            operation.Security.Add(requirement);
        }
    }
}
