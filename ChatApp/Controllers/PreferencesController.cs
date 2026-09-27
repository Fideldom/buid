using ChatApp.Data;
using ChatApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Controllers;

[Authorize]
[ApiController]
[Route("api/preferences")]
public class PreferencesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    public PreferencesController(ApplicationDbContext db, UserManager<ApplicationUser> users) { _db = db; _users = users; }
    private async Task<UserSettings?> Settings() { var u = await _users.GetUserAsync(User); return u == null ? null : await _db.UserSettings.FirstOrDefaultAsync(x => x.UserId == u.Id); }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var s = await Settings(); if (s == null) return Unauthorized();
        return Ok(new { theme=s.Theme, accentColor=s.AccentColor, uiDensity=s.UiDensity, reduceMotion=s.ReduceMotion, notifications=new { messages=s.NotifyMessages,calls=s.NotifyCalls,meetings=s.NotifyMeetings,friendRequests=s.NotifyFriendRequests,groups=s.NotifyGroups,sounds=s.NotifySounds,browser=s.NotifyBrowser } });
    }

    public class UpdateDto
    {
        public string? Theme { get; set; } public string? AccentColor { get; set; } public string? UiDensity { get; set; } public bool? ReduceMotion { get; set; }
        public bool? NotifyMessages { get; set; } public bool? NotifyCalls { get; set; } public bool? NotifyMeetings { get; set; } public bool? NotifyFriendRequests { get; set; } public bool? NotifyGroups { get; set; } public bool? NotifySounds { get; set; } public bool? NotifyBrowser { get; set; }
    }

    [HttpPut]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(UpdateDto dto)
    {
        var s = await Settings(); if (s == null) return Unauthorized();
        if (dto.Theme != null && new[] { "system", "light", "dark" }.Contains(dto.Theme)) s.Theme=dto.Theme;
        if (dto.AccentColor != null && System.Text.RegularExpressions.Regex.IsMatch(dto.AccentColor, "^#[0-9a-fA-F]{6}$")) s.AccentColor=dto.AccentColor;
        if (dto.UiDensity != null && new[] { "compact", "comfortable", "spacious" }.Contains(dto.UiDensity)) s.UiDensity=dto.UiDensity;
        if (dto.ReduceMotion.HasValue) s.ReduceMotion=dto.ReduceMotion.Value;
        if (dto.NotifyMessages.HasValue) s.NotifyMessages=dto.NotifyMessages.Value;
        if (dto.NotifyCalls.HasValue) s.NotifyCalls=dto.NotifyCalls.Value;
        if (dto.NotifyMeetings.HasValue) s.NotifyMeetings=dto.NotifyMeetings.Value;
        if (dto.NotifyFriendRequests.HasValue) s.NotifyFriendRequests=dto.NotifyFriendRequests.Value;
        if (dto.NotifyGroups.HasValue) s.NotifyGroups=dto.NotifyGroups.Value;
        if (dto.NotifySounds.HasValue) s.NotifySounds=dto.NotifySounds.Value;
        if (dto.NotifyBrowser.HasValue) s.NotifyBrowser=dto.NotifyBrowser.Value;
        s.UpdatedAt=DateTime.UtcNow; await _db.SaveChangesAsync(); return Ok(new { success=true });
    }
}
