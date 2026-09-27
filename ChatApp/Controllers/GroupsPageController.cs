using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ChatApp.Controllers;
[Authorize]
public class GroupsPageController : Controller
{
    [HttpGet("/Groups")]
    public IActionResult Index() => View("~/Views/Home/Groups.cshtml");
}

[Authorize]
public class StatusPageController : Controller
{
    [HttpGet("/Status")]
    public IActionResult Index() => View("~/Views/Home/Status.cshtml");

    [HttpGet("/Groups/{id:guid}")]
    public IActionResult Group(Guid id) { ViewBag.GroupId = id; return View("~/Views/Home/Group.cshtml"); }
}
