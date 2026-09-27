namespace ChatApp.Models;

public class ChatGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PhotoUrl { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public ApplicationUser Owner { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
    public ICollection<GroupMessage> Messages { get; set; } = new List<GroupMessage>();
    public ICollection<GroupInvite> Invites { get; set; } = new List<GroupInvite>();
}

public enum GroupRole { Member = 0, Admin = 1, Owner = 2 }
public enum GroupInviteStatus { Pending = 0, Accepted = 1, Rejected = 2, Cancelled = 3 }

public class GroupMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public ChatGroup Group { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public GroupRole Role { get; set; } = GroupRole.Member;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

public class GroupInvite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public ChatGroup Group { get; set; } = null!;
    public string InvitedUserId { get; set; } = string.Empty;
    public ApplicationUser InvitedUser { get; set; } = null!;
    public string InvitedById { get; set; } = string.Empty;
    public ApplicationUser InvitedBy { get; set; } = null!;
    public GroupInviteStatus Status { get; set; } = GroupInviteStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class GroupMessage
{
    public long Id { get; set; }
    public Guid GroupId { get; set; }
    public ChatGroup Group { get; set; } = null!;
    public string SenderId { get; set; } = string.Empty;
    public ApplicationUser Sender { get; set; } = null!;
    public string? Content { get; set; }
    public MessageType Type { get; set; } = MessageType.Text;
    public bool IsDeleted { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
