
using chatable.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Supabase;
using Supabase.Gotrue;
using System;
using System.Text;


var builder = WebApplication.CreateBuilder(args);
var url = "https://goexjtmckylmpnrbxtcn.supabase.co";
var key = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImdvZXhqdG1ja3lsbXBucmJ4dGNuIiwicm9sZSI6ImFub24iLCJpYXQiOjE2OTkxNzQ5MTgsImV4cCI6MjAxNDc1MDkxOH0.DPYagpce-yxXg6jTgyDYBcSHUEfputsGR-Z0e5sQKIk";
// Add services to the container.

builder.Services.AddControllers().AddNewtonsoftJson();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<Supabase.Client>(_ =>

    new Supabase.Client(
            url, key,
            new SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = true,
            }
));

var secretKey = builder.Configuration["AppSettings:SecretKey"];
var secretKeyBytes = Encoding.UTF8.GetBytes(secretKey);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = false,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(secretKeyBytes),
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorizationBuilder().AddPolicy("owner", p =>
{
    p.RequireClaim("UserName");
    //p.RequireClaim("TokenId", Guid.NewGuid().ToString());
    p.RequireRole("owner");
});
//builder.Services.AddAuthorization(options =>
//{
//    options.AddPolicy("owner", p => p.RequireClaim("TokenId"));
//});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
//var options = new Supabase.SupabaseOptions
//{
//    AutoConnectRealtime = true
//};
app.MapControllers();

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.Run();