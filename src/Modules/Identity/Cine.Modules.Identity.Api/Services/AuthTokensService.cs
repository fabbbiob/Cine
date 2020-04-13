using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Cine.Modules.Identity.Api.DTO;
using Cine.Modules.Identity.Api.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace Cine.Modules.Identity.Api.Services
{
    internal sealed class AuthTokensService : IAuthTokensService
    {
        private readonly IdentityOptions _options;
        private readonly IRefreshTokensService _refreshTokensService;
        private readonly IMemoryCache _cache;

        public AuthTokensService(IdentityOptions options, IRefreshTokensService refreshTokensService, IMemoryCache cache)
        {
            _options = options;
            _refreshTokensService = refreshTokensService;
            _cache = cache;
        }

        public async Task<AuthDto> CreateAsync(string username)
        {
            var handler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Issuer = _options.Issuer,
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, username),
                    new Claim(ClaimTypes.Role, "user"),
                }),
                Expires = DateTime.UtcNow.AddDays(_options.ExpirationDays),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(_options.Key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var securityToken = handler.CreateToken(tokenDescriptor);
            var refreshToken = await _refreshTokensService.CreateAsync(username);

            var token = new AuthDto
            {
                Token = handler.WriteToken(securityToken),
                RefreshToken = refreshToken,
                Issuer = securityToken.Issuer,
                ValidFrom = securityToken.ValidFrom,
                ValidTo = securityToken.ValidTo
            };

            _cache.Set(username, token, TimeSpan.FromSeconds(30));
            return token;
        }

        public bool Validate(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var signingKey = new SymmetricSecurityKey(_options.Key);
            try
            {
                var jwt = handler.ReadToken(token);

                handler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidateAudience = false,
                    ValidIssuer = jwt.Issuer,
                    IssuerSigningKey = signingKey
                }, out _);
            }
            catch
            {
                return false;
            }
            return true;
        }

        public AuthDto GetToken(string username)
            => _cache.Get<AuthDto>(username);
    }
}
