using AssetTracking.Rfid.Api.Models;
using AssetTracking.Rfid.Domain.Entities;
using AssetTracking.Rfid.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SouthernBotanical.Rfid.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/users")]

public class AdminUsersController : ControllerBase

{

    private readonly AppDbContext _db;
    private readonly JwtToken _jwtToken;

    public AdminUsersController(AppDbContext db, JwtToken jwtToken)
    {
        _db = db;
        _jwtToken = jwtToken;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _db.Users
            .Include(u => u.UserSiteRoles!)
                .ThenInclude(usr => usr.Site)
            .Include(u => u.UserSiteRoles!)
                .ThenInclude(usr => usr.Role)
            .ToListAsync();

        var result = users.Select(u => new
        {
            u.UserId,
            u.UserName,
            u.Email,
            u.PhoneNo,

            SiteRoles = u.UserSiteRoles.Select(usr => new
            {
                usr.SiteId,
                SiteName = usr.Site!.Name,
                usr.RoleId,
                RoleName = usr.Role!.Name,
                usr.IsActive
            })
        });

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("{siteId}")]
    public async Task<IActionResult> CreateUser(Guid siteId, [FromBody] CreateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (request.Password != request.ConfirmPassword)
            return BadRequest("Password and ConfirmPassword do not match.");

        bool emailExists = await _db.Users
            .AnyAsync(u => u.Email == request.Email);

        if (emailExists)
            return BadRequest("Email already exists.");

        bool roleExists = await _db.Roles
            .AnyAsync(r => r.RoleId == request.RoleId);

        if (!roleExists)
            return BadRequest("Invalid RoleId.");

        bool siteExists = await _db.Sites
            .AnyAsync(s => s.SiteId == siteId);

        if (!siteExists)
            return BadRequest("Invalid SiteId.");

        var user = new User
        {
            UserId = Guid.NewGuid(),
            UserName = request.UserName.Trim(),
            FirstName = request.FirstName?.Trim() ?? string.Empty,
            LastName = request.LastName?.Trim() ?? string.Empty,
            Email = request.Email.Trim().ToLower(),
            PhoneNo = request.PhoneNo,
            Password = HashPassword(request.Password),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);

        var userSiteRole = new UserSiteRole
        {
            UserSiteRoleId = Guid.NewGuid(),
            UserId = user.UserId,
            SiteId = siteId,
            RoleId = request.RoleId,
            AssignedAt = DateTime.UtcNow,
            IsActive = true
        };

        _db.UserSiteRoles.Add(userSiteRole);

        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(CreateUser), new
        {
            user.UserId,
            user.UserName,
            user.Email,
            user.PhoneNo,
            SiteId = siteId,
            request.RoleId
        });
    }

    [AllowAnonymous]
    [HttpPut("{userId}/{siteId}")]
    public async Task<IActionResult> UpdateUser(
        Guid userId,
        Guid siteId,
        [FromBody] UpdateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null)
            return NotFound("User not found.");

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            user.FirstName = request.FullName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.UserName))
            user.UserName = request.UserName.Trim();

        if (!string.IsNullOrWhiteSpace(request.PhoneNo))
            user.PhoneNo = request.PhoneNo.Trim();

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            bool emailExists = await _db.Users
                .AnyAsync(u => u.Email == request.Email && u.UserId != userId);

            if (emailExists)
                return BadRequest("Email already exists.");

            user.Email = request.Email.Trim().ToLower();
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (request.Password != request.ConfirmPassword)
                return BadRequest("Password and ConfirmPassword do not match.");

            user.Password = HashPassword(request.Password);
        }

        if (request.RoleId.HasValue)
        {
            bool roleExists = await _db.Roles
                .AnyAsync(r => r.RoleId == request.RoleId.Value);

            if (!roleExists)
                return BadRequest("Invalid RoleId.");

            var userSiteRole = await _db.UserSiteRoles
                .FirstOrDefaultAsync(x => x.UserId == userId && x.SiteId == siteId);

            if (userSiteRole == null)
                return NotFound("User does not have a role assigned for this site.");

            userSiteRole.RoleId = request.RoleId.Value;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            user.UserId,
            user.UserName,
            user.Email,
            user.PhoneNo,
            SiteId = siteId,
            RoleId = request.RoleId
        });
    }

    private string HashPassword(string password)
    {
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }

    [AllowAnonymous]
    [HttpPut("{UserId:guid}/role")]
    public async Task<ActionResult<User>> UpdateUserRole(
        Guid UserId,
        [FromBody] UpdateUserRoleRequest request)
    {
        var user = await _db.Users.FindAsync(UserId);

        if (user == null)
            return NotFound(new { message = "User not found." });

        var userSiteRole = await _db.UserSiteRoles
            .FirstOrDefaultAsync(x => x.UserId == UserId && x.SiteId == request.SiteId);

        if (userSiteRole == null)
            return NotFound(new { message = "User does not have a role for this site." });

        userSiteRole.RoleId = request.RoleId;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "User role updated successfully for the site.",
            userId = UserId,
            siteId = request.SiteId,
            roleId = request.RoleId
        });
    }


    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.Email == request.Email);

        if (user == null)
            return BadRequest(new { message = "User does not exist." });

        var hashedInputPassword = HashPassword(request.Password);

        if (user.Password != hashedInputPassword)
            return BadRequest(new { message = "Invalid password." });

        var userSiteRole = await _db.UserSiteRoles
            .Include(x => x.Role)
            .Include(x => x.Site)
            .Where(x => x.UserId == user.UserId && x.IsActive)
            .OrderBy(x => x.AssignedAt)
            .FirstOrDefaultAsync();

        if (userSiteRole == null)
            return BadRequest(new { message = "User has no assigned site." });

        var token = _jwtToken.GenerateJwtToken(
            user,
            userSiteRole.SiteId,
            userSiteRole.Role?.Name ?? string.Empty
        );

        return Ok(new
        {
            message = "Login successful.",
            token,
            user = new
            {
                user.UserId,
                user.UserName,
                user.Email,
                SiteId = userSiteRole.SiteId,
                RoleId = userSiteRole.RoleId,
                RoleName = userSiteRole.Role?.Name
            }
        });
    }

    [AllowAnonymous]
    [HttpPost("SiteWiseToken/{userId}/{siteId}")]
    public async Task<IActionResult> SiteWiseToken(Guid userId, Guid siteId)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (user == null)
            return BadRequest(new { message = "User does not exist." });

        var userSiteRole = await _db.UserSiteRoles
            .Include(x => x.Role)
            .Include(x => x.Site)
            .FirstOrDefaultAsync(x => x.UserId == userId &&
                                      x.SiteId == siteId &&
                                      x.IsActive);

        if (userSiteRole == null)
            return BadRequest(new { message = "User has no access to this site." });

        var token = _jwtToken.GenerateJwtToken(
            user,
            userSiteRole.SiteId,
            userSiteRole.Role?.Name ?? string.Empty
        );

        return Ok(new
        {
            message = "Site‑wise token generated successfully.",
            token,
            user = new
            {
                user.UserId,
                user.UserName,
                user.Email,
                SiteId = userSiteRole.SiteId,
                SiteName = userSiteRole.Site?.Name,
                userSiteRole.RoleId,
                RoleName = userSiteRole.Role?.Name
            }
        });
    }
}

