using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PainFinder.Application.Services;
using PainFinder.Domain.Entities;
using PainFinder.Shared.Contracts;
using PainFinder.Shared.DTOs;

namespace PainFinder.Infrastructure.Services;

public class AuthService(
    UserManager<AppUser> userManager,
    IConfiguration configuration,
    ISubscriptionService subscriptionService) : IAuthService
{
    public async Task<AuthResponseDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var existingUser = await userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
            throw new InvalidOperationException("A user with this email already exists.");

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName,
            Email = request.Email,
            UserName = request.Email,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Registration failed: {errors}");
        }

        await subscriptionService.EnsurePlanExistsAsync(user.Id, cancellationToken);

        var (token, expiresAt) = GenerateJwtToken(user);

        return new AuthResponseDto(token, expiresAt, ToUserDto(user, "Free"));
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            throw new InvalidOperationException("Invalid email or password.");

        var validPassword = await userManager.CheckPasswordAsync(user, request.Password);
        if (!validPassword)
            throw new InvalidOperationException("Invalid email or password.");

        await subscriptionService.EnsurePlanExistsAsync(user.Id, cancellationToken);
        var plan = await subscriptionService.GetUserPlanAsync(user.Id, cancellationToken);
        var (token, expiresAt) = GenerateJwtToken(user);

        return new AuthResponseDto(token, expiresAt, ToUserDto(user, plan.PlanType));
    }

    public async Task<AuthResponseDto> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken cancellationToken = default)
    {
        var googleClientId = configuration["Google:ClientId"]
            ?? throw new InvalidOperationException("Google ClientId not configured.");

        GoogleJsonWebSignature.Payload payload;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [googleClientId]
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
        }
        catch (InvalidJwtException)
        {
            throw new InvalidOperationException("Invalid Google token.");
        }

        var email = payload.Email
            ?? throw new InvalidOperationException("Google account does not have an email.");

        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            user = new AppUser
            {
                Id = Guid.NewGuid(),
                FullName = payload.Name ?? email.Split('@')[0],
                Email = email,
                UserName = email,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Account creation failed: {errors}");
            }
        }

        await subscriptionService.EnsurePlanExistsAsync(user.Id, cancellationToken);
        var plan = await subscriptionService.GetUserPlanAsync(user.Id, cancellationToken);
        var (token, expiresAt) = GenerateJwtToken(user);

        return new AuthResponseDto(token, expiresAt, ToUserDto(user, plan.PlanType));
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return null;
        var plan = await subscriptionService.GetUserPlanAsync(userId, cancellationToken);
        return ToUserDto(user, plan.PlanType);
    }

    private (string Token, DateTime ExpiresAt) GenerateJwtToken(AppUser user)
    {
        var jwtKey = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT Key not configured.");
        var issuer = configuration["Jwt:Issuer"] ?? "PainFinder";
        var audience = configuration["Jwt:Audience"] ?? "PainFinder";
        var expirationHours = int.TryParse(configuration["Jwt:ExpirationInHours"], out var h) ? h : 24;

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("fullName", user.FullName)
        };

        var expiresAt = DateTime.UtcNow.AddHours(expirationHours);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private static UserDto ToUserDto(AppUser user, string planType = "Free") =>
        new(user.Id, user.FullName, user.Email ?? string.Empty, user.CreatedAt, planType);
}
