using AssetTracking.Rfid.Domain.Entities;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AssetTracking.Rfid.Api
{
    public class JwtToken
    {
        private readonly IConfiguration _configuration;

        public JwtToken(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateJwtToken(User user, string roleName)
        {
            var jwtSettings = _configuration.GetSection("Jwt");

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["Key"])
            );

            var claims = new[]
                          {
                      new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
                      new Claim(JwtRegisteredClaimNames.Email, user.Email),
                      new Claim("UserName", user.FullName),
                      new Claim("Role", roleName ?? "User"),
                      new Claim("Site", user.SiteId.ToString()),
                      new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                  };

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(
                    Convert.ToDouble(jwtSettings["ExpireMinutes"])
                ),
                signingCredentials: new SigningCredentials(
                    key, SecurityAlgorithms.HmacSha256
                )
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}





