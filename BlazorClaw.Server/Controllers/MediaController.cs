using BlazorClaw.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlazorClaw.Server.Controllers;

[ApiController]
[AllowAnonymous]
public class MediaController(PathHelper pathHelper) : ControllerBase
{
    [HttpGet("/uploads/{fileName}")]
    public async Task<ActionResult> GetMediaFile(string fileName)
    {
        var t = pathHelper.GetMediaFile(fileName);
        if (t == null) return NotFound();
        return File(t.GetStream(), t.MimeType, t.FileName, true);
    }
}