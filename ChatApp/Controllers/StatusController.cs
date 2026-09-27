using ChatApp.Data;
using ChatApp.DTOs;
using ChatApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ChatApp.Controllers;

[Authorize]
[ApiController]
[Route("api/status")]
public class StatusController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public StatusController(ApplicationDbContext db) => _db = db;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var now = DateTime.UtcNow;
        var friendIds = await _db.Friendships.Where(f => f.Status == FriendshipStatus.Accepted && (f.RequesterId == UserId || f.AddresseeId == UserId))
            .Select(f => f.RequesterId == UserId ? f.AddresseeId : f.RequesterId).ToListAsync();
        friendIds.Add(UserId);
        var statuses = await _db.Statuses.AsNoTracking().Include(s => s.User).Include(s => s.Views)
            .Where(s => s.ExpiresAt > now && friendIds.Contains(s.UserId)).OrderByDescending(s => s.CreatedAt).ToListAsync();
        return Ok(statuses.Select(s => new StatusResponseDto(s.Id, s.UserId, s.User.FullName, s.User.ProfilePhotoUrl, s.Text, s.MediaUrl, s.MediaType, s.CreatedAt, s.ExpiresAt, s.Views.Count, s.Views.Any(v => v.UserId == UserId))));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateStatusDto dto)
    {
        var text = string.IsNullOrWhiteSpace(dto.Text) ? null : dto.Text.Trim();
        var mediaUrl = string.IsNullOrWhiteSpace(dto.MediaUrl) ? null : dto.MediaUrl.Trim();
        var mediaType = string.IsNullOrWhiteSpace(dto.MediaType) ? "none" : dto.MediaType.Trim().ToLowerInvariant();

        if (text is null && mediaUrl is null)
            return BadRequest(new { message = "Adiciona texto ou media ao estado." });

        if (text?.Length > 1000)
            return BadRequest(new { message = "O texto do estado é demasiado longo." });

        if (mediaType is not ("none" or "image" or "video"))
            return BadRequest(new { message = "Tipo de media inválido." });

        if (mediaUrl is not null)
        {
            if (!mediaUrl.StartsWith("/uploads/status/", StringComparison.OrdinalIgnoreCase) || mediaUrl.Length > 2048)
                return BadRequest(new { message = "A media do estado deve ser enviada pelo sistema de upload." });

            if (mediaType == "none")
                return BadRequest(new { message = "Indica o tipo da media." });
        }

        var status = new Status
        {
            UserId = UserId,
            Text = text,
            MediaUrl = mediaUrl,
            MediaType = mediaType,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };

        _db.Statuses.Add(status);
        await _db.SaveChangesAsync();
        return Ok(new { success = true, id = status.Id });
    }

    [HttpPost("{id:int}/view")]
    public async Task<IActionResult> ViewStatus(int id)
    {
        var status = await _db.Statuses.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.ExpiresAt > DateTime.UtcNow);

        if (status == null) return NotFound();

        if (status.UserId != UserId)
        {
            var isFriend = await _db.Friendships.AnyAsync(f =>
                f.Status == FriendshipStatus.Accepted &&
                ((f.RequesterId == status.UserId && f.AddresseeId == UserId) ||
                 (f.RequesterId == UserId && f.AddresseeId == status.UserId)));

            if (!isFriend)
                return Forbid();

            var alreadyViewed = await _db.StatusViews.AnyAsync(v => v.StatusId == id && v.UserId == UserId);
            if (!alreadyViewed)
            {
                _db.StatusViews.Add(new StatusView { StatusId = id, UserId = UserId, ViewedAt = DateTime.UtcNow });
                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // Outra requisição concorrente pode ter criado a mesma visualização.
                }
            }
        }

        var count = await _db.StatusViews.CountAsync(v => v.StatusId == id);
        return Ok(new { success = true, viewCount = count });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var status = await _db.Statuses.FirstOrDefaultAsync(s => s.Id == id && s.UserId == UserId);
        if (status == null) return NotFound();
        _db.Statuses.Remove(status);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }
}
