using AssetTracking.Rfid.Domain.Entities;
using AssetTracking.Rfid.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetTracking.Rfid.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/readers")]
public class AdminReadersController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminReadersController(AppDbContext db)
    {
        _db = db;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Reader>>> GetAll()
    {
        var list = await _db.Readers.ToListAsync();
        return Ok(list);
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<ActionResult<Reader>> Create([FromBody] Reader reader)
    {
        if (reader.ReaderId == Guid.Empty)
            reader.ReaderId = Guid.NewGuid();

        _db.Readers.Add(reader);
        await _db.SaveChangesAsync();
        return Ok(reader);
    }

    [AllowAnonymous]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Reader>> Update(Guid id, [FromBody] Reader request)
    {
        var reader = await _db.Readers.FirstOrDefaultAsync(r => r.ReaderId == id);
        if (reader is null) return NotFound();

        reader.Name = request.Name;
        reader.Location = request.Location;
        reader.IsActive = request.IsActive;

        await _db.SaveChangesAsync();
        return Ok(reader);
    }
}
