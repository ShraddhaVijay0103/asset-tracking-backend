using AssetTracking.Rfid.Api;
using AssetTracking.Rfid.Api.Models;
using AssetTracking.Rfid.Domain.Entities;
using AssetTracking.Rfid.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;

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
            .Include(u => u.Role)
            .Include(u => u.Site)
            .ToListAsync();

        var result = users.Select(u => new
        {
            u.UserId,
            u.FullName,
            u.UserName,
            u.PhoneNo,
            u.Email,
            u.Password,
            u.ConfirmPassword,
            u.Site.SiteId,
            u.Site.Name,
            u.Role.RoleId,
            RoleName = u.Role.Name

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
            FullName = request.FullName.Trim(),
            UserName = request.UserName.Trim(),
            PhoneNo = request.PhoneNo,
            Email = request.Email.Trim().ToLower(),
            Password = HashPassword(request.Password),
            ConfirmPassword = HashPassword(request.ConfirmPassword),
            SiteId = siteId,
            RoleId = request.RoleId
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(CreateUser), new
        {
            user.UserId,
            user.FullName,
            user.UserName,
            user.PhoneNo,
            user.Email,
            user.SiteId,
            user.RoleId
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
            .FirstOrDefaultAsync(u => u.UserId == userId && u.SiteId == siteId);

        if (user == null)
            return NotFound("User not found for the given site.");

        // Full Name
        if (!string.IsNullOrWhiteSpace(request.FullName))
            user.FullName = request.FullName;

        // Username
        if (!string.IsNullOrWhiteSpace(request.UserName))
            user.UserName = request.UserName;

        // Phone
        if (!string.IsNullOrWhiteSpace(request.PhoneNo))
            user.PhoneNo = request.PhoneNo;

        // Email
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            bool emailExists = await _db.Users
                .AnyAsync(u => u.Email == request.Email && u.UserId != userId);

            if (emailExists)
                return BadRequest("Email already exists.");

            user.Email = request.Email;
        }

        // Password
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (request.Password != request.ConfirmPassword)
                return BadRequest("Password and ConfirmPassword do not match.");

            user.Password = HashPassword(request.Password);
        }

        // Role
        if (request.RoleId.HasValue)
        {
            bool roleExists = await _db.Roles
                .AnyAsync(r => r.RoleId == request.RoleId.Value);

            if (!roleExists)
                return BadRequest("Invalid RoleId.");

            user.RoleId = request.RoleId.Value;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            user.UserId,
            user.FullName,
            user.UserName,
            user.PhoneNo,
            user.Email,
            user.SiteId,
            user.RoleId
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
    [HttpPut("{id:guid}/role")]
    public async Task<ActionResult<User>> UpdateUserRole(Guid id, [FromBody] UpdateUserRoleRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id);

        if (user is null) return NotFound();

        user.RoleId = request.RoleId;

        await _db.SaveChangesAsync();

        return Ok(user);

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

        var role = await _db.Roles
            .FirstOrDefaultAsync(x => x.RoleId == user.RoleId);

        var token = _jwtToken.GenerateJwtToken(user, role?.Name);

        return Ok(new
        {
            message = "Login successful.",
            token,
            user = new
            {
                user.UserId,
                user.FullName,
                user.Email,
                user.SiteId,
                user.RoleId,
                RoleName = role?.Name
            }
        });
    }
}

