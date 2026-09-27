using ChatApp.Data;
using ChatApp.DTOs.Groups;
using ChatApp.Hubs;
using ChatApp.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Services;

public class GroupService
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IHubContext<ChatHub> _chatHub;

    public GroupService(ApplicationDbContext db, INotificationService notifications, IHubContext<ChatHub> chatHub)
    {
        _db = db;
        _notifications = notifications;
        _chatHub = chatHub;
    }

    public async Task<ChatGroup> CreateAsync(string userId, CreateGroupDto dto)
    {
        var name = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length < 2 || name.Length > 120)
            throw new ArgumentException("O nome do grupo deve ter entre 2 e 120 caracteres.");

        var description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        if (description?.Length > 500)
            throw new ArgumentException("A descrição do grupo é demasiado longa.");

        var photoUrl = string.IsNullOrWhiteSpace(dto.PhotoUrl) ? null : dto.PhotoUrl.Trim();
        if (photoUrl is not null && !photoUrl.StartsWith("/uploads/images/", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A foto do grupo deve ser enviada pelo sistema de upload.");

        var group = new ChatGroup
        {
            Name = name,
            Description = description,
            PhotoUrl = photoUrl,
            OwnerId = userId
        };
        group.Members.Add(new GroupMember { GroupId = group.Id, UserId = userId, Role = GroupRole.Owner });
        _db.ChatGroups.Add(group);
        await _db.SaveChangesAsync();

        foreach (var memberId in (dto.MemberIds ?? new List<string>()).Where(x => x != userId).Distinct())
        {
            if (!await _db.Users.AnyAsync(u => u.Id == memberId)) continue;
            await InviteAsync(userId, group.Id, memberId);
        }
        return group;
    }

    public async Task<List<GroupResponseDto>> GetMineAsync(string userId)
    {
        var groups = await _db.ChatGroups.AsNoTracking()
            .Where(g => g.Members.Any(m => m.UserId == userId))
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => new GroupResponseDto(g.Id, g.Name, g.Description, g.PhotoUrl, g.OwnerId, g.CreatedAt,
                g.Members.Count, true, g.Members.Where(m => m.UserId == userId).Select(m => m.Role.ToString()).FirstOrDefault()))
            .ToListAsync();
        return groups;
    }

    public Task<bool> IsMemberAsync(Guid groupId, string userId)
        => _db.GroupMembers.AsNoTracking().AnyAsync(m => m.GroupId == groupId && m.UserId == userId);

    public async Task<ChatGroup?> GetAsync(Guid id, string userId)
    {
        return await _db.ChatGroups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == id && g.Members.Any(m => m.UserId == userId));
    }

    public async Task<List<GroupMemberDto>> MembersAsync(Guid id, string userId)
    {
        if (!await _db.GroupMembers.AnyAsync(m => m.GroupId == id && m.UserId == userId))
            throw new UnauthorizedAccessException("Não és membro deste grupo.");

        return await _db.GroupMembers.AsNoTracking().Where(m => m.GroupId == id)
            .OrderByDescending(m => m.Role).ThenBy(m => m.User.FullName)
            .Select(m => new GroupMemberDto(m.UserId, m.User.FullName, m.User.UserName, m.User.ProfilePhotoUrl, m.Role.ToString(), m.User.IsOnline))
            .ToListAsync();
    }

    public async Task<GroupInvite> InviteAsync(string actorId, Guid groupId, string userId)
    {
        var actor = await _db.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == actorId);
        if (actor == null || actor.Role == GroupRole.Member) throw new UnauthorizedAccessException("Sem permissão para convidar membros.");
        if (!await _db.Users.AnyAsync(u => u.Id == userId)) throw new KeyNotFoundException("Utilizador não encontrado.");
        if (await _db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId)) throw new InvalidOperationException("O utilizador já pertence ao grupo.");
        var pending = await _db.GroupInvites.FirstOrDefaultAsync(i => i.GroupId == groupId && i.InvitedUserId == userId && i.Status == GroupInviteStatus.Pending);
        if (pending != null) return pending;
        var invite = new GroupInvite { GroupId = groupId, InvitedUserId = userId, InvitedById = actorId };
        _db.GroupInvites.Add(invite);
        await _db.SaveChangesAsync();
        var group = await _db.ChatGroups.FindAsync(groupId);
        await _notifications.CreateAsync(userId, NotificationType.GroupInvite, "Convite para grupo", $"Foste convidado para o grupo {group?.Name ?? "grupo"}.", groupId.ToString());
        return invite;
    }

    public async Task AcceptInviteAsync(string userId, Guid groupId)
    {
        var invite = await _db.GroupInvites.FirstOrDefaultAsync(i => i.GroupId == groupId && i.InvitedUserId == userId && i.Status == GroupInviteStatus.Pending);
        if (invite == null) throw new KeyNotFoundException("Convite não encontrado.");
        invite.Status = GroupInviteStatus.Accepted;
        if (!await _db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId))
            _db.GroupMembers.Add(new GroupMember { GroupId = groupId, UserId = userId });
        await _db.SaveChangesAsync();
        await _chatHub.Clients.Group(ChatHub.UserGroup(userId)).SendAsync("GroupInviteAccepted", groupId.ToString());
    }

    public async Task RemoveMemberAsync(string actorId, Guid groupId, string userId)
    {
        var actor = await _db.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == actorId);
        if (actor == null || actor.Role == GroupRole.Member) throw new UnauthorizedAccessException("Sem permissão.");
        var member = await _db.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);
        if (member == null) return;
        if (member.Role == GroupRole.Owner) throw new InvalidOperationException("O proprietário não pode ser removido.");
        _db.GroupMembers.Remove(member);
        await _db.SaveChangesAsync();
        await _notifications.CreateAsync(userId, NotificationType.GroupMemberRemoved, "Removido do grupo", "Foste removido de um grupo.", groupId.ToString());
    }

    public async Task<GroupMessageDto> SendMessageAsync(string userId, Guid groupId, string? content)
    {
        if (!await _db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId))
            throw new UnauthorizedAccessException("Não és membro deste grupo.");
        if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("A mensagem não pode estar vazia.");
        content = content.Trim();
        if (content.Length > 10000) throw new ArgumentException("A mensagem é demasiado longa.");
        var msg = new GroupMessage { GroupId = groupId, SenderId = userId, Content = content };
        _db.GroupMessages.Add(msg);
        await _db.SaveChangesAsync();
        var sender = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
        return new GroupMessageDto(msg.Id, groupId, userId, sender.FullName, sender.ProfilePhotoUrl, msg.Content, msg.Type.ToString(), msg.SentAt, false);
    }

    public async Task<List<GroupMessageDto>> MessagesAsync(string userId, Guid groupId, int take = 50)
    {
        if (!await _db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId)) throw new UnauthorizedAccessException("Sem acesso.");
        return await _db.GroupMessages.AsNoTracking().Where(m => m.GroupId == groupId).OrderByDescending(m => m.SentAt).Take(Math.Clamp(take, 1, 100))
            .OrderBy(m => m.SentAt).Select(m => new GroupMessageDto(m.Id, m.GroupId, m.SenderId, m.Sender.FullName, m.Sender.ProfilePhotoUrl, m.Content, m.Type.ToString(), m.SentAt, m.IsDeleted)).ToListAsync();
    }
    public async Task RejectInviteAsync(string userId, Guid groupId)
    {
        var invite = await _db.GroupInvites.FirstOrDefaultAsync(i => i.GroupId == groupId && i.InvitedUserId == userId && i.Status == GroupInviteStatus.Pending);
        if (invite == null) throw new KeyNotFoundException("Convite não encontrado.");
        invite.Status = GroupInviteStatus.Rejected;
        await _db.SaveChangesAsync();
    }

    public async Task<object> UpdateSettingsAsync(string userId, Guid groupId, UpdateGroupSettingsDto dto)
    {
        var group = await _db.ChatGroups.FirstOrDefaultAsync(g => g.Id == groupId);
        if (group == null) throw new KeyNotFoundException("Grupo não encontrado.");
        if (group.OwnerId != userId) throw new UnauthorizedAccessException("Apenas o proprietário pode alterar as configurações.");
        var name = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length < 2 || name.Length > 120) throw new ArgumentException("Nome inválido.");
        if (dto.Description?.Length > 500) throw new ArgumentException("Descrição demasiado longa.");
        if (!string.IsNullOrWhiteSpace(dto.PhotoUrl) && !dto.PhotoUrl.StartsWith("/uploads/images/", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Foto inválida.");
        group.Name = name; group.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(); group.PhotoUrl = string.IsNullOrWhiteSpace(dto.PhotoUrl) ? null : dto.PhotoUrl.Trim();
        await _db.SaveChangesAsync();
        return new { id=group.Id, name=group.Name, description=group.Description, photoUrl=group.PhotoUrl };
    }

}
