using ChatApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChatApp.Controllers;

[Authorize]
[ApiController]
[Route("api/uploads")]
public class UploadsController : ControllerBase
{
    private readonly IFileStorageService _storage;
    public UploadsController(IFileStorageService storage) => _storage = storage;

    [HttpPost("image")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public Task<IActionResult> Image(IFormFile file) => Save(file, "images", 10, new[] { "image/jpeg", "image/png", "image/webp", "image/gif" });

    [HttpPost("status-media")]
    [RequestSizeLimit(60 * 1024 * 1024)]
    public Task<IActionResult> StatusMedia(IFormFile file) => Save(file, "status", 60, new[] { "image/jpeg", "image/png", "image/webp", "image/gif", "video/mp4", "video/webm", "video/quicktime" });

    private async Task<IActionResult> Save(IFormFile? file, string folder, int maxMb, string[] contentTypes)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Nenhum ficheiro foi selecionado." });
        if (!contentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { message = "Formato de ficheiro não suportado." });
        if (file.Length > maxMb * 1024L * 1024L)
            return BadRequest(new { message = $"O ficheiro excede o limite de {maxMb} MB." });

        try
        {
            var saved = await _storage.SaveFileAsync(file, folder);
            return Ok(new { url = saved.Url, fileName = saved.FileName, size = saved.Size, contentType = file.ContentType });
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
