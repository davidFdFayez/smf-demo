using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace SMF.Api.Auth;

public sealed class JwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public string Issue(Guid refereeId, string displayName, IEnumerable<string> roles)
    {
        var keyBytes = Encoding.UTF8.GetBytes(_options.SigningKey);
        var signing = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            // The hub's Context.UserIdentifier is sourced from this claim.
            new(JwtRegisteredClaimNames.Sub, refereeId.ToString()),
            new(ClaimTypes.NameIdentifier, refereeId.ToString()),
            new(JwtRegisteredClaimNames.Name, displayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_options.TokenLifetimeMinutes),
            signingCredentials: signing);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
