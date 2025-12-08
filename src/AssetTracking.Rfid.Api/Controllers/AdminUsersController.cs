using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AssetTracking.Rfid.Domain.Entities;
using AssetTracking.Rfid.Infrastructure.Persistence;
using AssetTracking.Rfid.Api.Models;

namespace AssetTracking.Rfid.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminUsersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<User>>> GetUsers()
    {
        var list = await _db.Users
            .Include(u => u.Role)
            .ToListAsync();

        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult<User>> CreateUser([FromBody] CreateUserRequest request)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Name = request.Name,
            Email = request.Email,
            RoleId = request.RoleId
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Ok(user);
    }

    [HttpPut("{id:guid}/role")]
    public async Task<ActionResult<User>> UpdateUserRole(Guid id, [FromBody] UpdateUserRoleRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id);
        if (user is null) return NotFound();

        user.RoleId = request.RoleId;
        await _db.SaveChangesAsync();
        return Ok(user);
    }
}
