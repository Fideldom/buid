using ChatApp.DTOs.Groups;
using ChatApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ChatApp.Controllers;

[Authorize]
[ApiController]
[Route("api/groups")]
public class GroupsController : ControllerBase
{
    private readonly GroupService _groups;
    public GroupsController(GroupService groups) => _groups = groups;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGroupDto dto)
    {
        try
        {
            var group = await _groups.CreateAsync(UserId, dto);
            return Ok(new
            {
                id = group.Id,
                name = group.Name,
                description = group.Description,
                photoUrl = group.PhotoUrl,
                ownerId = group.OwnerId,
                createdAt = group.CreatedAt,
                memberCount = 1
            });
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (DbUpdateException ex)
        {
            return StatusCode(500, new { message = "Não foi possível guardar o grupo na base de dados." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Ocorreu um erro ao criar o grupo." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Mine() => Ok(await _groups.GetMineAsync(UserId));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var group = await _groups.GetAsync(id, UserId);
        return group == null ? NotFound(new { message = "Grupo não encontrado." }) : Ok(new { id = group.Id, name = group.Name, description = group.Description, photoUrl = group.PhotoUrl, ownerId = group.OwnerId, createdAt = group.CreatedAt, memberCount = group.Members.Count });
    }

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> Members(Guid id)
    {
        try { return Ok(await _groups.MembersAsync(id, UserId)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }

    [HttpGet("{id:guid}/messages")]
    public async Task<IActionResult> Messages(Guid id, [FromQuery] int take = 50)
    {
        try { return Ok(await _groups.MessagesAsync(UserId, id, take)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/invite")]
    public async Task<IActionResult> Invite(Guid id, GroupInviteDto dto)
    {
        try { return Ok(await _groups.InviteAsync(UserId, id, dto.UserId)); }
        catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException or InvalidOperationException) { return BadRequest(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id)
    {
        try { await _groups.AcceptInviteAsync(UserId, id); return Ok(new { success = true }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpDelete("{id:guid}/members/{userId}")]
    public async Task<IActionResult> Remove(Guid id, string userId)
    {
        try { await _groups.RemoveMemberAsync(UserId, id, userId); return Ok(new { success = true }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }
}
