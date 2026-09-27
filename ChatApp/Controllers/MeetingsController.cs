using ChatApp.Data;
using ChatApp.DTOs;
using ChatApp.Models;
using ChatApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Controllers;

// Reuniões / videoconferências: criação, convites, participantes,
// entrada por código de sala e encerramento.
// A mídia de áudio/vídeo é tratada pelo WebRTC/CallHub.
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MeetingsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notifications;

    public MeetingsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        INotificationService notifications)
    {
        _db = db;
        _userManager = userManager;
        _notifications = notifications;
    }

    private string? CurrentUserId => _userManager.GetUserId(User);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMeetingDto dto)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        if (dto == null)
            return BadRequest(new { message = "Dados da reunião são obrigatórios." });

        var title = dto.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(new { message = "O título da reunião é obrigatório." });

        if (title.Length > 150)
            return BadRequest(new { message = "O título da reunião é demasiado longo." });

        if (dto.ScheduledStart.HasValue && dto.ScheduledEnd.HasValue &&
            dto.ScheduledEnd.Value <= dto.ScheduledStart.Value)
        {
            return BadRequest(new { message = "O fim da reunião deve ser posterior ao início." });
        }

        var requestedInviteIds = (dto.InviteUserIds ?? new List<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Where(id => id != userId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var invitedUsers = requestedInviteIds.Count == 0
            ? new List<ApplicationUser>()
            : await _db.Users
                .Where(u => requestedInviteIds.Contains(u.Id))
                .ToListAsync();

        var existingIds = invitedUsers
            .Select(u => u.Id)
            .ToHashSet(StringComparer.Ordinal);

        var invalidIds = requestedInviteIds
            .Where(id => !existingIds.Contains(id))
            .ToList();

        if (invalidIds.Count > 0)
        {
            return BadRequest(new
            {
                message = "Um ou mais participantes selecionados não existem.",
                invalidUserIds = invalidIds
            });
        }

        var host = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (host == null)
            return Unauthorized();

        var meeting = new Meeting
        {
            Title = title,
            HostId = userId,
            ScheduledStart = dto.ScheduledStart,
            ScheduledEnd = dto.ScheduledEnd,
            Status = MeetingStatus.Scheduled,
            CreatedAt = DateTime.UtcNow
        };

        // O anfitrião entra automaticamente na reunião.
        meeting.Participants.Add(new MeetingParticipant
        {
            UserId = userId,
            IsHost = true
        });

        // Todos os usuários selecionados são colocados na lista de participantes.
        foreach (var invitedUser in invitedUsers)
        {
            meeting.Participants.Add(new MeetingParticipant
            {
                UserId = invitedUser.Id,
                IsHost = false
            });
        }

        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        // Cada participante convidado recebe uma notificação persistente.
        // Ela aparece na aba Reuniões, não no centro geral de notificações.
        foreach (var invitedUser in invitedUsers)
        {
            await _notifications.CreateAsync(
                invitedUser.Id,
                NotificationType.MeetingInvite,
                $"Convite para reunião: {meeting.Title}",
                $"{host.FullName} convidou-te para participar de uma reunião.",
                meeting.RoomCode);
        }

        return Ok(new MeetingViewDto
        {
            Id = meeting.Id,
            Title = meeting.Title,
            RoomCode = meeting.RoomCode,
            HostId = meeting.HostId,
            HostName = host.FullName,
            ScheduledStart = meeting.ScheduledStart,
            ScheduledEnd = meeting.ScheduledEnd,
            Status = meeting.Status.ToString()
        });
    }

    [HttpGet("upcoming")]
    public async Task<IActionResult> GetUpcoming()
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var meetings = await _db.MeetingParticipants
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.Meeting)
            .Where(m =>
                m.Status != MeetingStatus.Ended &&
                m.Status != MeetingStatus.Cancelled)
            .OrderBy(m => m.ScheduledStart)
            .ThenByDescending(m => m.CreatedAt)
            .Select(m => new MeetingViewDto
            {
                Id = m.Id,
                Title = m.Title,
                RoomCode = m.RoomCode,
                HostId = m.HostId,
                HostName = m.Host.FullName,
                ScheduledStart = m.ScheduledStart,
                ScheduledEnd = m.ScheduledEnd,
                Status = m.Status.ToString()
            })
            .ToListAsync();

        return Ok(meetings);
    }

    [HttpGet("{roomCode}")]
    public async Task<IActionResult> GetByRoomCode(string roomCode)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(roomCode))
            return BadRequest();

        var meeting = await _db.Meetings
            .AsNoTracking()
            .Include(m => m.Host)
            .Include(m => m.Participants)
            .FirstOrDefaultAsync(m => m.RoomCode == roomCode);

        if (meeting == null)
            return NotFound();

        if (!meeting.Participants.Any(p => p.UserId == userId))
            return Forbid();

        return Ok(new MeetingViewDto
        {
            Id = meeting.Id,
            Title = meeting.Title,
            RoomCode = meeting.RoomCode,
            HostId = meeting.HostId,
            HostName = meeting.Host.FullName,
            ScheduledStart = meeting.ScheduledStart,
            ScheduledEnd = meeting.ScheduledEnd,
            Status = meeting.Status.ToString()
        });
    }

    [HttpPost("{roomCode}/join")]
    public async Task<IActionResult> Join(string roomCode)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var meeting = await _db.Meetings
            .Include(m => m.Participants)
            .FirstOrDefaultAsync(m => m.RoomCode == roomCode);

        if (meeting == null)
            return NotFound();

        if (meeting.Status == MeetingStatus.Ended ||
            meeting.Status == MeetingStatus.Cancelled)
        {
            return BadRequest(new { message = "Esta reunião já terminou." });
        }

        var participant = meeting.Participants
            .FirstOrDefault(p => p.UserId == userId);

        if (participant == null)
            return Forbid();

        participant.JoinedAt ??= DateTime.UtcNow;
        participant.LeftAt = null;

        if (meeting.Status == MeetingStatus.Scheduled)
        {
            meeting.Status = MeetingStatus.Ongoing;
            meeting.ActualStart ??= DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            meeting.Id,
            meeting.RoomCode,
            status = meeting.Status.ToString()
        });
    }

    [HttpPost("{roomCode}/leave")]
    public async Task<IActionResult> Leave(string roomCode)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var meeting = await _db.Meetings
            .Include(m => m.Participants)
            .FirstOrDefaultAsync(m => m.RoomCode == roomCode);

        if (meeting == null)
            return NotFound();

        var participant = meeting.Participants
            .FirstOrDefault(p => p.UserId == userId);

        if (participant == null)
            return Forbid();

        participant.LeftAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("{roomCode}/end")]
    public async Task<IActionResult> EndMeeting(string roomCode)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var meeting = await _db.Meetings
            .FirstOrDefaultAsync(m =>
                m.RoomCode == roomCode &&
                m.HostId == userId);

        if (meeting == null)
            return NotFound();

        if (meeting.Status == MeetingStatus.Ended)
            return Ok();

        meeting.Status = MeetingStatus.Ended;
        meeting.ActualEnd = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok();
    }
}
