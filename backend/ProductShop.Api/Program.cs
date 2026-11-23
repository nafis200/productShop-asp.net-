using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ProductShop.Application;
using ProductShop.Domain.Constants;
using ProductShop.Infrastructure;
using ProductShop.Infrastructure.Auth;
using ProductShop.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// =====================================================================
// ধাপ ১ — সার্ভিস রেজিস্ট্রেশন (DI কন্টেইনারে কোন কোন ক্লাস থাকবে)
// =====================================================================

// কী করে: কন্ট্রোলার চালু করে।
// কীভাবে করে: AuthorizeFilter সব এন্ডপয়েন্টে টোকেন বাধ্যতামূলক করে দেয়;
//              শুধু [AllowAnonymous] দেওয়া এন্ডপয়েন্ট (register, login) টোকেন ছাড়া চলে।
builder.Services.AddControllers(options => options.Filters.Add(new AuthorizeFilter()));

// কী করে: /swagger পেজ তৈরি করে, যেখান থেকে API টেস্ট করা যায়।
// কীভাবে করে: "Bearer" সিকিউরিটি স্কিম যোগ করে, ফলে Swagger-এ "Authorize" বাটন আসে
//              এবং সেখানে JWT বসালে প্রতিটি রিকোয়েস্টের হেডারে টোকেন চলে যায়।
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Name = "Authorization",
        Description = "Paste the accessToken returned by /api/auth/login."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// কী করে: Application ও Infrastructure লেয়ারের সার্ভিসগুলো রেজিস্টার করে।
// কীভাবে করে: প্রতিটি লেয়ারের DependencyInjection.cs ফাইলের এক্সটেনশন মেথড কল করে।
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// কী করে: JWT অথেনটিকেশন চালু করে।
// কীভাবে করে: রিকোয়েস্টের "Authorization: Bearer <token>" হেডার থেকে টোকেন পড়ে যাচাই করে।
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// কী করে: টোকেন যাচাইয়ের নিয়ম ঠিক করে।
// কীভাবে করে: JwtTokenService যে JwtSettings দিয়ে টোকেন সাইন করে, সেই একই সেটিংস দিয়ে যাচাই করে,
//              তাই সাইন আর যাচাইয়ের মধ্যে কখনো অমিল হয় না।
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<JwtSettings>((options, jwtSettings) =>
    {
        // ক্লেইমের নাম টোকেনে যেমন আছে তেমনই রাখে ("sub", "email", "role") — বদলে ফেলে না।
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            // টোকেন কে ইস্যু করেছে (Issuer) এবং কার জন্য (Audience) — দুটোই মিলিয়ে দেখে।
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,

            // সিক্রেট কী দিয়ে সিগনেচার যাচাই করে; শুধু HmacSha256 অ্যালগরিদম গ্রহণ করে।
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },

            // মেয়াদ শেষ হলে টোকেন বাতিল; সার্ভারের ঘড়ির পার্থক্যের জন্য ৩০ সেকেন্ড ছাড় দেয়।
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),

            // User.Identity.Name হিসেবে "email" ক্লেইম ব্যবহার করে।
            NameClaimType = JwtRegisteredClaimNames.Email,
            // [Authorize(Roles = "Admin")] কোন ক্লেইম থেকে রোল পড়বে তা বলে দেয়।
            RoleClaimType = RoleNames.ClaimType
        };
    });

builder.Services.AddAuthorization();

// কী করে: Next.js ফ্রন্টএন্ড (যেমন http://localhost:3000) থেকে এই API কল করার অনুমতি দেয়।
// কীভাবে করে: appsettings-এর "Cors:AllowedOrigins" থেকে অনুমোদিত অরিজিনগুলো পড়ে পলিসি বানায়;
//              অন্য কোনো অরিজিন থেকে আসা কল ব্রাউজার ব্লক করে দেয়।
const string FrontendCorsPolicy = "Frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

// =====================================================================
// ধাপ ২ — অ্যাপ চালুর সময়ের কাজ
// =====================================================================

// কী করে: ডাটাবেস মাইগ্রেশন চালায় এবং অ্যাডমিন ইউজার তৈরি করে।
// কীভাবে করে: একটি স্কোপ খুলে DatabaseInitializer নেয়; কনফিগে
//              "Database:ApplyMigrationsOnStartup" true থাকলে মাইগ্রেশন অ্যাপ্লাই করে।
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync(app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"));
}

// =====================================================================
// ধাপ ৩ — মিডলওয়্যার পাইপলাইন (প্রতিটি রিকোয়েস্ট এই ক্রমেই এগুলোর ভেতর দিয়ে যায়)
// =====================================================================

// কী করে: ডেভেলপমেন্টে Swagger দেখায়; প্রোডাকশনে HTTP থেকে HTTPS-এ রিডাইরেক্ট করে।
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors(FrontendCorsPolicy);   // অথের আগে রাখতে হয়, যাতে preflight (OPTIONS) রিকোয়েস্টও CORS হেডার পায়
app.UseAuthentication();           // "তুমি কে?" — JWT পড়ে ইউজার চিনে নেয়
app.UseAuthorization();            // "তোমার অনুমতি আছে?" — [Authorize] ও Roles যাচাই করে

app.MapControllers();              // সবশেষে রিকোয়েস্টটি সঠিক কন্ট্রোলার মেথডে পাঠায়

app.Run();
