namespace ChatApp.DTOs;

public record CreateStatusDto(string? Text, string? MediaUrl, string? MediaType);
public record StatusResponseDto(int Id, string UserId, string UserName, string? UserPhotoUrl, string? Text, string? MediaUrl, string MediaType, DateTime CreatedAt, DateTime ExpiresAt, int ViewCount, bool Viewed);
