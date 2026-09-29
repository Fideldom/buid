using ChatApp.DTOs;
using ChatApp.Models;
using ChatApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ChatApp.Controllers;

// Criação de conta com e-mail, nome, palavra-passe e foto de perfil, mais login/logout.
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IFileStorageService _fileStorage;

    public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IFileStorageService fileStorage)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _fileStorage = fileStorage;
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        if (!ModelState.IsValid) return View(dto);

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FullName = dto.FullName
        };

        if (dto.Photo != null)
        {
            try
            {
                var (url, _, _) = await _fileStorage.SaveFileAsync(dto.Photo, "photos");
                user.ProfilePhotoUrl = url;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                ModelState.AddModelError(nameof(dto.Photo), ex.Message);
                return View(dto);
            }
        }

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(dto);
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        if (!ModelState.IsValid) return View(dto);

        var result = await _signInManager.PasswordSignInAsync(dto.Email, dto.Password, dto.RememberMe, lockoutOnFailure: true);
        if (result.RequiresTwoFactor)
        {
            return RedirectToAction(nameof(TwoFactorLogin), new
            {
                rememberMe = dto.RememberMe,
                returnUrl = Url.Action("Index", "Home")
            });
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "A conta está temporariamente bloqueada. Tenta novamente mais tarde.");
            return View(dto);
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "E-mail ou palavra-passe inválidos.");
            return View(dto);
        }

        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult TwoFactorLogin(bool rememberMe = false, string? returnUrl = null)
    {
        ViewData["RememberMe"] = rememberMe;
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TwoFactorLogin(TwoFactorLoginDto dto)
    {
        if (!ModelState.IsValid)
        {
            ViewData["RememberMe"] = dto.RememberMe;
            ViewData["ReturnUrl"] = dto.ReturnUrl;
            return View(dto);
        }

        var code = dto.Code.Trim().Replace(" ", string.Empty).Replace("-", string.Empty);
        var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(
            code,
            dto.RememberMe,
            rememberClient: false);

        // if (!result.Succeeded && code.Contains("-", StringComparison.Ordinal))
        // {
        //     result = await _signInManager.TwoFactorRecoveryCodeSignInAsync(
        //         code,
        //         dto.RememberMe);
        // }

        if (result.Succeeded)
        {
            if (!string.IsNullOrWhiteSpace(dto.ReturnUrl) && Url.IsLocalUrl(dto.ReturnUrl))
                return Redirect(dto.ReturnUrl);

            return RedirectToAction("Index", "Home");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "A conta está temporariamente bloqueada.");
        }
        else
        {
            ModelState.AddModelError(string.Empty, "O código de autenticação é inválido.");
        }

        ViewData["RememberMe"] = dto.RememberMe;
        ViewData["ReturnUrl"] = dto.ReturnUrl;
        return View(dto);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }

    [Authorize]
    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UpdatePhoto(IFormFile photo)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        try
        {
            var (url, _, _) = await _fileStorage.SaveFileAsync(photo, "photos");
            if (!string.IsNullOrEmpty(user.ProfilePhotoUrl))
                _fileStorage.DeleteFile(user.ProfilePhotoUrl);

            user.ProfilePhotoUrl = url;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(new { message = "Não foi possível atualizar a foto de perfil." });

            return Ok(new { url });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
