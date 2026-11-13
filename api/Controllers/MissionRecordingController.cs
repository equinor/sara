using System.Text.RegularExpressions;
using api.Controllers.Models;
using api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("recordings/{installationCode}/missions/{runId}")]
[Authorize(Policy = InstallationAccess.AdminPolicy)]
public partial class MissionRecordingController(IMissionRecordingService missionRecordingService)
    : ControllerBase
{
    // Route values end up in blob paths, so only plain identifiers are accepted.
    [GeneratedRegex("^[A-Za-z0-9]{2,10}$")]
    private static partial Regex InstallationCodePattern();

    [GeneratedRegex("^[A-Za-z0-9_-]{1,128}$")]
    private static partial Regex CameraKeyPattern();

    [HttpGet]
    [ProducesResponseType(typeof(MissionRecordingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<MissionRecordingResponse>> GetRecording(
        [FromRoute] string installationCode,
        [FromRoute] string runId
    )
    {
        if (!IsValid(installationCode, runId))
            return BadRequest("Invalid installation code or mission id");

        return Ok(await missionRecordingService.GetRecording(installationCode, runId));
    }

    [HttpGet("cameras/{cameraKey}/access")]
    [ProducesResponseType(typeof(RecordingAccessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecordingAccessResponse>> GetAccess(
        [FromRoute] string installationCode,
        [FromRoute] string runId,
        [FromRoute] string cameraKey
    )
    {
        if (!IsValid(installationCode, runId) || !CameraKeyPattern().IsMatch(cameraKey))
            return BadRequest("Invalid installation code, mission id or camera key");

        var access = await missionRecordingService.GetAccess(installationCode, runId, cameraKey);
        return access is null ? NotFound("The recording is not available yet") : Ok(access);
    }

    private static bool IsValid(string installationCode, string runId) =>
        InstallationCodePattern().IsMatch(installationCode)
        && Guid.TryParseExact(runId, "D", out _);
}
