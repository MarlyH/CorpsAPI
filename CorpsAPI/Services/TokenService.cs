using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CorpsAPI.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace CorpsAPI.Services
{
    public class TokenService
    {
        private readonly IMemoryCache _memoryCache;

        public TokenService(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        public string GenerateAccessToken(AppUser user, SigningCredentials credentials, IList<string> roles)
        {
            var accessClaims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, string.Join(",", roles))
        };

            var accessToken = new JwtSecurityToken(
                issuer: "corps-api-access",
                audience: "corps-app-access",
                claims: accessClaims,
                expires: DateTime.Now.AddMinutes(15),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(accessToken);
        }

        public string GenerateRefreshToken(IdentityUser user, SigningCredentials credentials)
        {
            var jti = Guid.NewGuid().ToString();
            var refreshClaims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.Now.ToUnixTimeSeconds().ToString())
        };

            var refreshToken = new JwtSecurityToken(
                issuer: "corps-api-refresh",
                audience: "corps-app-refresh",
                claims: refreshClaims,
                expires: DateTime.Now.AddDays(7),
                signingCredentials: credentials
            );

            //_memoryCache.Set(jti, user.Id, TimeSpan.FromDays(7));

            return new JwtSecurityTokenHandler().WriteToken(refreshToken);
        }
    }
}
