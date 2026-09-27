using System.Text.RegularExpressions;

namespace ChatApp.Services;

public class FileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;
    private readonly long _maxSizeBytes;

    public FileStorageService(IWebHostEnvironment env, IConfiguration config)
    {
        _env = env;
        var maxMb = config.GetValue<int?>("FileStorage:MaxFileSizeMb") ?? 50;
        _maxSizeBytes = Math.Clamp(maxMb, 1, 250) * 1024L * 1024L;
    }

    public async Task<(string Url, string FileName, long Size)> SaveFileAsync(IFormFile file, string subFolder)
    {
        if (file is null || file.Length <= 0)
            throw new ArgumentException("Ficheiro vazio.");

        if (file.Length > _maxSizeBytes)
            throw new InvalidOperationException($"Ficheiro excede o tamanho máximo permitido ({_maxSizeBytes / (1024 * 1024)} MB).");

        if (string.IsNullOrWhiteSpace(subFolder) ||
            !Regex.IsMatch(subFolder, "^[a-zA-Z0-9_-]+$"))
            throw new ArgumentException("Destino de ficheiro inválido.");

        var (extension, detectedContentType) = await DetectFileTypeAsync(file);
        if (extension is null || detectedContentType is null)
            throw new InvalidOperationException("O conteúdo do ficheiro não corresponde a um formato de imagem ou vídeo suportado.");

        var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads", subFolder);
        Directory.CreateDirectory(uploadsRoot);

        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(uploadsRoot, storedFileName);

        await using (var input = file.OpenReadStream())
        await using (var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true))
        {
            await input.CopyToAsync(output);
        }

        var relativeUrl = $"/uploads/{subFolder}/{storedFileName}";
        return (relativeUrl, Path.GetFileName(file.FileName), file.Length);
    }

    private static async Task<(string? Extension, string? ContentType)> DetectFileTypeAsync(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        var header = new byte[16];
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length));

        if (read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return (".png", "image/png");

        if (read >= 3 && header.AsSpan(0, 3).SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF }))
            return (".jpg", "image/jpeg");

        if (read >= 6 && (header.AsSpan(0, 6).SequenceEqual("GIF87a"u8) || header.AsSpan(0, 6).SequenceEqual("GIF89a"u8)))
            return (".gif", "image/gif");

        if (read >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            return (".webp", "image/webp");

        if (read >= 12 && header.AsSpan(4, 4).SequenceEqual("ftyp"u8))
        {
            var brand = System.Text.Encoding.ASCII.GetString(header, 8, 4);
            if (brand is "isom" or "iso2" or "mp41" or "mp42" or "avc1" or "M4V " or "MSNV")
                return (".mp4", "video/mp4");
            if (brand is "qt  ")
                return (".mov", "video/quicktime");
        }

        if (read >= 4 && header.AsSpan(0, 4).SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }))
            return (".webm", "video/webm");

        return (null, null);
    }

    public void DeleteFile(string relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl)) return;
        if (!relativeUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)) return;

        var relative = relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(Path.Combine(_env.WebRootPath, "uploads"));
        var path = Path.GetFullPath(Path.Combine(_env.WebRootPath, relative));

        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return;
        if (File.Exists(path)) File.Delete(path);
    }
}
