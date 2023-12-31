
using chatable.Hubs;
using chatable.Models;
using chatable.Services;
using EmailService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Supabase;
using Supabase.Gotrue;
using System;
using System.Configuration;
using System.Text;


var builder = WebApplication.CreateBuilder(args);
var url = "https://goexjtmckylmpnrbxtcn.supabase.co";
var key = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImdvZXhqdG1ja3lsbXBucmJ4dGNuIiwicm9sZSI6ImFub24iLCJpYXQiOjE2OTkxNzQ5MTgsImV4cCI6MjAxNDc1MDkxOH0.DPYagpce-yxXg6jTgyDYBcSHUEfputsGR-Z0e5sQKIk";
// Add services to the container.

builder.Services.AddControllers().AddNewtonsoftJson();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

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
        opt.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/messages-hub")))
                {
                    context.Token = accessToken;
                }
                else
                {
                    string authorizationHeader = context.Request.Headers["Authorization"];
                    if (!string.IsNullOrEmpty(authorizationHeader))
                    {
                        string token = authorizationHeader.Substring("Bearer ".Length).Trim();
                        context.Token = token;
                    }
                }

                return Task.CompletedTask;
            }
        };
    });
var emailConfig = builder.Configuration.GetSection("EmailConfiguration")
  .Get<EmailConfiguration>();

builder.Services.AddSingleton(emailConfig);
builder.Services.AddScoped<IEmailSender, EmailSender>();

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapHub<MessagesHub>("messages-hub");
app.MapHub<CallHub>("call-hub");
app.MapHub<RoomHub>("room-hub");

app.Run();