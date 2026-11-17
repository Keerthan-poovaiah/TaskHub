using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TaskHub.Core;

namespace TaskHub.Api.Auth;

// The stamping tool: makes login tickets and knows how to check them
public class TokenService
{
    private const string Issuer = "taskhub-api";
    private const string Audience = "taskhub-clients";

    private readonly SymmetricSecurityKey _signingKey;

    public TokenService(string secret)
    {
        if (secret.Length < 32)
            throw new InvalidOperationException(
                "JWT_SECRET is missing or too short. Put a random string of at least 32 characters in your .env file.");

        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }

    public int LifetimeMinutes => 60;

    // The rules the API uses to check every incoming ticket
    public TokenValidationParameters ValidationParameters => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        ValidateLifetime = true,                 // reject expired tickets
        ValidateIssuerSigningKey = true,         // reject forged stamps
        IssuerSigningKey = _signingKey,
        ClockSkew = TimeSpan.FromSeconds(30)
    };

    // Makes a stamped ticket that says "this is user #N"
    public string CreateToken(User user)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", user.Id.ToString()),
                new Claim("email", user.Email)
            }),
            Issuer = Issuer,
            Audience = Audience,
            Expires = DateTime.UtcNow.AddMinutes(LifetimeMinutes),
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}