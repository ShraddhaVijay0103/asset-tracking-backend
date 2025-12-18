using AssetTracking.Rfid.Api.Models;
using AssetTracking.Rfid.Domain.Entities;
using AssetTracking.Rfid.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SouthernBotanical.Rfid.Api.Controllers;

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

            FullName = request.FullName,

            UserName = request.UserName,

            PhoneNo = request.PhoneNo,

            Email = request.Email,

            Password = request.Password, // NOTE: ideally you hash it

            ConfirmPassword = request.ConfirmPassword,

            SiteId = request.SiteId,

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

    [HttpPost("login")]

    public async Task<IActionResult> Login([FromBody] LoginRequest request)

    {

        var user = await _db.Users

            .FirstOrDefaultAsync(x => x.Email == request.Email);

        if (user == null)

        {

            return BadRequest(new { message = "User does not exist." });

        }

        if (user.Password != request.Password)

        {

            return BadRequest(new { message = "Invalid password." });

        }


        return Ok(new

        {

            message = "Login successful.",

            user = new

            {

                user.UserId,

                user.Email,

                user.RoleId

            }

        });

    }
   
}

