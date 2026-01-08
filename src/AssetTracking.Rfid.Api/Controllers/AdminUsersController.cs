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
    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserMultiSiteRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (request.Password != request.ConfirmPassword)
            return BadRequest("Password and ConfirmPassword do not match.");

        bool emailExists = await _db.Users
            .AnyAsync(u => u.Email == request.Email);

        if (emailExists)
            return BadRequest("Email already exists.");

        if (request.Site == null || !request.Site.Any())
            return BadRequest("At least one site must be provided.");

        var validSites = await _db.Sites
            .Where(s => request.Site.Contains(s.SiteId))
            .Select(s => s.SiteId)
            .ToListAsync();

        if (validSites.Count != request.Site.Count)
            return BadRequest("One or more SiteIds are invalid.");

        if (request.Role == null || !request.Role.Any())
            return BadRequest("At least one role must be provided.");

        var validRoles = await _db.Roles
            .Where(r => request.Role.Contains(r.RoleId))
            .Select(r => r.RoleId)
            .ToListAsync();

        if (validRoles.Count != request.Role.Count)
            return BadRequest("One or more RoleIds are invalid.");

        var user = new User
        {
            UserId = Guid.NewGuid(),
            UserName = request.UserName.Trim(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email.Trim().ToLower(),
            PhoneNo = request.PhoneNo,
            Password = HashPassword(request.Password),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);

        int siteCount = validSites.Count;
        int roleCount = validRoles.Count;

        for (int i = 0; i < siteCount; i++)
        {
            Guid currentSite = validSites[i];
            Guid currentRole;

            if (i < roleCount)
            {
                currentRole = validRoles[i];
            }
            else
            {
                currentRole = validRoles.Last();
            }

            var userSiteRole = new UserSiteRole
            {
                UserSiteRoleId = Guid.NewGuid(),
                UserId = user.UserId,
                SiteId = currentSite,
                RoleId = currentRole,
                AssignedAt = DateTime.UtcNow,
                IsActive = true
            };

            _db.UserSiteRoles.Add(userSiteRole);
        }

        await _db.SaveChangesAsync();

        return Ok(new { Message = "User created successfully", userId = user.UserId });
    }

    [AllowAnonymous]
    [HttpPut("{userId:guid}")]
    public async Task<IActionResult> UpdateUser(
     Guid userId,
     [FromBody] UpdateUserMultiSiteRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (user == null)
            return NotFound("User not found.");

        /* ---------- USER FIELDS ---------- */

        if (!string.IsNullOrWhiteSpace(request.UserName))
        {
            bool userNameExists = await _db.Users
                .AnyAsync(u => u.UserName == request.UserName && u.UserId != userId);

            if (userNameExists)
                return BadRequest("Username already exists.");

            user.UserName = request.UserName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.FirstName))
            user.FirstName = request.FirstName.Trim();

        if (!string.IsNullOrWhiteSpace(request.LastName))
            user.LastName = request.LastName.Trim();

        if (!string.IsNullOrWhiteSpace(request.PhoneNo))
            user.PhoneNo = request.PhoneNo.Trim();

        /* ---------- PASSWORD ---------- */

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (request.Password != request.ConfirmPassword)
                return BadRequest("Password and ConfirmPassword do not match.");

            user.Password = HashPassword(request.Password);
        }

        // Ignore Email completely
        // user.Email is never updated

        user.UpdatedAt = DateTime.UtcNow;

        /* ---------- SITE & ROLE ---------- */

        if (request.Site == null || request.Site.Count == 0)
            return BadRequest("At least one SiteId is required.");

        if (request.Role == null || request.Role.Count == 0)
            return BadRequest("At least one RoleId is required.");

        var validSites = await _db.Sites
            .Where(s => request.Site.Contains(s.SiteId))
            .Select(s => s.SiteId)
            .ToListAsync();

        if (validSites.Count != request.Site.Count)
            return BadRequest("Invalid SiteId found.");

        var validRoles = await _db.Roles
            .Where(r => request.Role.Contains(r.RoleId))
            .Select(r => r.RoleId)
            .ToListAsync();

        if (validRoles.Count != request.Role.Count)
            return BadRequest("Invalid RoleId found.");

        var existingMappings = await _db.UserSiteRoles
            .Where(x => x.UserId == userId)
            .ToListAsync();

        _db.UserSiteRoles.RemoveRange(existingMappings);

        for (int i = 0; i < validSites.Count; i++)
        {
            _db.UserSiteRoles.Add(new UserSiteRole
            {
                UserSiteRoleId = Guid.NewGuid(),
                UserId = userId,
                SiteId = validSites[i],
                RoleId = i < validRoles.Count ? validRoles[i] : validRoles.First(),
                IsActive = true,
                AssignedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Message = "User updated successfully",
            UserId = user.UserId
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

