namespace ChatApp.DTOs.Groups;

public record CreateGroupDto(string Name, string? Description, string? PhotoUrl, List<string>? MemberIds);
public record UpdateGroupDto(string Name, string? Description, string? PhotoUrl);
public record GroupMemberDto(string UserId, string FullName, string? UserName, string? ProfilePhotoUrl, string Role, bool IsOnline);
public record GroupResponseDto(Guid Id, string Name, string? Description, string? PhotoUrl, string OwnerId, DateTime CreatedAt, int MemberCount, bool IsMember, string? MyRole);
public record GroupMessageDto(long Id, Guid GroupId, string SenderId, string SenderName, string? SenderPhotoUrl, string? Content, string Type, DateTime SentAt, bool IsDeleted);
public record GroupInviteDto(Guid GroupId, string UserId);


public class UpdateGroupSettingsDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PhotoUrl { get; set; }
}
