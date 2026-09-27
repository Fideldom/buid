using ChatApp.Data;
using ChatApp.Hubs;
using ChatApp.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<ChatHub> _hub;

    public NotificationService(
        ApplicationDbContext db,
        IHubContext<ChatHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task<Notification> CreateAsync(
        string userId,
        NotificationType type,
        string title,
        string? content = null,
        string? relatedEntityId = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException(
                "O utilizador da notificação é obrigatório.",
                nameof(userId));

        var userExists = await _db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId);

        if (!userExists)
            throw new KeyNotFoundException("Utilizador da notificação não encontrado.");

        var now = DateTime.UtcNow;

        // A notificação é sempre persistida. As preferências controlam a entrega
        // em tempo real, não a existência do histórico no banco de dados.
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = string.IsNullOrWhiteSpace(title) ? "Notificação" : title.Trim(),
            Content = string.IsNullOrWhiteSpace(content) ? null : content.Trim(),
            RelatedEntityId = string.IsNullOrWhiteSpace(relatedEntityId)
                ? null
                : relatedEntityId.Trim(),
            IsRead = false,
            CreatedAt = now
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        var settings = await _db.UserSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId);

        // Preferências controlam a entrega em tempo real.
        // O registro continua disponível no banco para histórico/contador.
        if (ShouldDeliverRealtime(settings, type))
        {
            await _hub.Clients
                .Group(ChatHub.UserGroup(userId))
                .SendAsync("ReceiveNotification", new
                {
                    notification.Id,
                    Type = notification.Type.ToString(),
                    notification.Title,
                    notification.Content,
                    notification.RelatedEntityId,
                    notification.IsRead,
                    notification.CreatedAt
                });
        }

        return notification;
    }

    private static bool ShouldDeliverRealtime(
        UserSettings? settings,
        NotificationType type)
    {
        if (settings == null)
            return true;

        return type switch
        {
            NotificationType.NewMessage => settings.NotifyMessages,
            NotificationType.IncomingCall or NotificationType.MissedCall
                => settings.NotifyCalls,
            NotificationType.MeetingInvite
                => settings.NotifyMeetings,
            NotificationType.FriendRequest or NotificationType.FriendAccepted
                => settings.NotifyFriendRequests,
            NotificationType.GroupInvite or
            NotificationType.GroupMemberAdded or
            NotificationType.GroupMemberRemoved
                => settings.NotifyGroups,
            _ => true
        };
    }
}
