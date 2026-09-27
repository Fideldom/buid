using ChatApp.Data;
using ChatApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    private string? CurrentUserId => _userManager.GetUserId(User);

    // O centro de notificações mostra EXCLUSIVAMENTE:
    // - chamadas perdidas
    // - convites para grupos
    // - convites para canais
    // Convites de reuniões ficam persistidos no banco, mas aparecem na aba Reuniões.
    private static IQueryable<Notification> UserCenterNotifications(
        IQueryable<Notification> query,
        string userId)
    {
        return query.Where(n =>
            n.UserId == userId &&
            (n.Type == NotificationType.MissedCall ||
             n.Type == NotificationType.GroupInvite ||
             n.Type == NotificationType.ChannelInvite));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool onlyUnread = false)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var query = UserCenterNotifications(_db.Notifications.AsNoTracking(), userId);

        if (onlyUnread)
            query = query.Where(n => !n.IsRead);

        var result = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new
            {
                n.Id,
                Type = n.Type.ToString(),
                n.Title,
                n.Content,
                n.RelatedEntityId,
                n.IsRead,
                n.CreatedAt
            })
            .ToListAsync();

        return Ok(result);
    }

    [HttpGet("count")]
    public async Task<IActionResult> GetNotificationCount()
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var count = await UserCenterNotifications(_db.Notifications, userId)
            .CountAsync(n => !n.IsRead);

        return Ok(new { count });
    }

    [HttpGet("messages-count")]
    public async Task<IActionResult> GetMessagesCount()
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var count = await _db.Messages
            .CountAsync(m =>
                m.ReceiverId == userId &&
                !m.IsRead &&
                !m.IsDeleted);

        return Ok(new { count });
    }

    [HttpGet("meetings-count")]
    public async Task<IActionResult> GetMeetingsCount()
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var count = await _db.Notifications
            .CountAsync(n =>
                n.UserId == userId &&
                !n.IsRead &&
                n.Type == NotificationType.MeetingInvite);

        return Ok(new { count });
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n =>
                n.Id == id &&
                n.UserId == userId);

        if (notification == null)
            return NotFound();

        notification.IsRead = true;
        await _db.SaveChangesAsync();

        return Ok(new { success = true });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var unread = await UserCenterNotifications(_db.Notifications, userId)
            .Where(n => !n.IsRead)
            .ToListAsync();

        if (unread.Count > 0)
        {
            foreach (var notification in unread)
                notification.IsRead = true;

            await _db.SaveChangesAsync();
        }

        return Ok(new { count = unread.Count });
    }

    [HttpPost("messages/read")]
    public async Task<IActionResult> MarkMessagesAsRead()
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var messages = await _db.Messages
            .Where(m =>
                m.ReceiverId == userId &&
                !m.IsRead &&
                !m.IsDeleted)
            .ToListAsync();

        var now = DateTime.UtcNow;

        foreach (var message in messages)
        {
            message.IsRead = true;
            message.ReadAt = now;
        }

        if (messages.Count > 0)
            await _db.SaveChangesAsync();

        return Ok(new { count = messages.Count });
    }

    [HttpPost("meetings/read")]
    public async Task<IActionResult> MarkMeetingsAsRead()
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var notifications = await _db.Notifications
            .Where(n =>
                n.UserId == userId &&
                !n.IsRead &&
                n.Type == NotificationType.MeetingInvite)
            .ToListAsync();

        foreach (var notification in notifications)
            notification.IsRead = true;

        if (notifications.Count > 0)
            await _db.SaveChangesAsync();

        return Ok(new { count = notifications.Count });
    }

    [HttpPost("messages/{friendId}/read")]
    public async Task<IActionResult> MarkConversationMessagesAsRead(string friendId)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(friendId))
            return BadRequest(new { message = "Utilizador inválido." });

        var messages = await _db.Messages
            .Where(m =>
                m.SenderId == friendId &&
                m.ReceiverId == userId &&
                !m.IsRead &&
                !m.IsDeleted)
            .ToListAsync();

        var now = DateTime.UtcNow;

        foreach (var message in messages)
        {
            message.IsRead = true;
            message.ReadAt = now;
        }

        if (messages.Count > 0)
            await _db.SaveChangesAsync();

        return Ok(new { count = messages.Count });
    }
}
