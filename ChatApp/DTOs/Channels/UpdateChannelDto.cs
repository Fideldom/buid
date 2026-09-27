namespace ChatApp.DTOs.Channels;

public class UpdateChannelDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PhotoUrl { get; set; }
    public bool IsPrivate { get; set; }
}
