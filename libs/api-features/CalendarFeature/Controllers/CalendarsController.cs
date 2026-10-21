using MicroMES.ApiFeatures.CalendarFeature.Models;
using MicroMES.ApiFeatures.CalendarFeature.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MicroMES.ApiFeatures.CalendarFeature.Controllers;

[ApiController]
[Tags("Calendars")]
[Produces("application/json")]
public class CalendarsController(CalendarService service, CalendarExceptionService exceptions) : ControllerBase
{
    [HttpGet("api/calendars", Name = "GetCalendars")]
    [ProducesResponseType<List<WorkCalendarModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCalendars([FromQuery] bool includeInactive = false)
        => ToResponse(await service.GetAllAsync(includeInactive), r => Ok(r.Data));

    [HttpGet("api/calendars/{id:int}", Name = "GetCalendar")]
    [ProducesResponseType<WorkCalendarModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCalendar(int id)
        => ToResponse(await service.GetAsync(id), r => Ok(r.Data));

    [HttpPost("api/calendars", Name = "CreateCalendar")]
    [ProducesResponseType<WorkCalendarModel>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateCalendar([FromBody] CreateWorkCalendarRequest request)
        => ToResponse(await service.CreateAsync(request), r => CreatedAtRoute("GetCalendar", new { id = r.Data!.Id }, r.Data));

    [HttpPut("api/calendars/{id:int}", Name = "UpdateCalendar")]
    [ProducesResponseType<WorkCalendarModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateCalendar(int id, [FromBody] UpdateWorkCalendarRequest request)
        => ToResponse(await service.UpdateAsync(id, request), r => Ok(r.Data));

    /// <summary>Copy of the calendar (same weekly slots, not default, no exceptions).</summary>
    [HttpPost("api/calendars/{id:int}/duplicate", Name = "DuplicateCalendar")]
    [ProducesResponseType<WorkCalendarModel>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DuplicateCalendar(int id, [FromBody] DuplicateWorkCalendarRequest request)
        => ToResponse(await service.DuplicateAsync(id, request), r => CreatedAtRoute("GetCalendar", new { id = r.Data!.Id }, r.Data));

    [HttpPost("api/calendars/{id:int}/set-default", Name = "SetDefaultCalendar")]
    [ProducesResponseType<WorkCalendarModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetDefaultCalendar(int id)
        => ToResponse(await service.SetDefaultAsync(id), r => Ok(r.Data));

    /// <summary>Soft-delete; 409 for the default calendar or one used by active machines.</summary>
    [HttpDelete("api/calendars/{id:int}", Name = "DeactivateCalendar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeactivateCalendar(int id)
        => ToResponse(await service.DeactivateAsync(id), _ => NoContent());

    // ---- time zone -------------------------------------------------------------------------------------

    [HttpGet("api/calendars/time-zone", Name = "GetCompanyTimeZone")]
    [ProducesResponseType<CompanyTimeZoneModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCompanyTimeZone()
        => ToResponse(await service.GetTimeZoneAsync(), r => Ok(r.Data));

    [HttpPut("api/calendars/time-zone", Name = "UpdateCompanyTimeZone")]
    [ProducesResponseType<CompanyTimeZoneModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateCompanyTimeZone([FromBody] UpdateCompanyTimeZoneRequest request)
        => ToResponse(await service.SetTimeZoneAsync(request), r => Ok(r.Data));

    // ---- preview and days -------------------------------------------------------------------------------

    /// <summary>Where a job of <paramref name="minutes"/> standard minutes starting at <paramref name="start"/> (UTC) ends.</summary>
    [HttpGet("api/calendars/{id:int}/preview", Name = "PreviewCalendar")]
    [ProducesResponseType<CalendarPreviewModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PreviewCalendar(int id, [FromQuery] DateTime start, [FromQuery] decimal minutes)
        => ToResponse(await service.PreviewCalendarAsync(id, start, minutes), r => Ok(r.Data));

    /// <summary>Same as the calendar preview with the machine's effective calendar, exceptions and efficiency.</summary>
    [HttpGet("api/equipment/{id:int}/calendar/preview", Name = "PreviewEquipmentCalendar")]
    [ProducesResponseType<CalendarPreviewModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PreviewEquipmentCalendar(int id, [FromQuery] DateTime start, [FromQuery] decimal minutes)
        => ToResponse(await service.PreviewEquipmentAsync(id, start, minutes), r => Ok(r.Data));

    /// <summary>Day-by-day status of a calendar (month view), at most 400 days.</summary>
    [HttpGet("api/calendars/{id:int}/days", Name = "GetCalendarDays")]
    [ProducesResponseType<List<CalendarDayModel>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetCalendarDays(int id, [FromQuery] DateOnly from, [FromQuery] DateOnly to)
        => ToResponse(await service.GetDaysAsync(id, null, from, to), r => Ok(r.Data));

    /// <summary>Day-by-day status of the machine's effective calendar (company exceptions and its own).</summary>
    [HttpGet("api/equipment/{id:int}/calendar/days", Name = "GetEquipmentCalendarDays")]
    [ProducesResponseType<List<CalendarDayModel>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetEquipmentCalendarDays(int id, [FromQuery] DateOnly from, [FromQuery] DateOnly to)
        => ToResponse(await service.GetDaysAsync(null, id, from, to), r => Ok(r.Data));

    // ---- calendar exceptions -----------------------------------------------------------------------------

    [HttpGet("api/calendars/{id:int}/exceptions", Name = "GetCalendarExceptions")]
    [ProducesResponseType<List<CalendarExceptionModel>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCalendarExceptions(int id, [FromQuery] int? year = null)
        => ToResponse(await exceptions.ListForCalendarAsync(id, year), r => Ok(r.Data));

    /// <summary>Overlaps with other exceptions of the calendar are not errors: they come back in <c>overlaps</c>.</summary>
    [HttpPost("api/calendars/{id:int}/exceptions", Name = "CreateCalendarException")]
    [ProducesResponseType<CalendarExceptionModel>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateCalendarException(int id, [FromBody] SaveCalendarExceptionRequest request)
        => ToResponse(await exceptions.CreateForCalendarAsync(id, request), r => Created($"api/calendars/{id}/exceptions/{r.Data!.Id}", r.Data));

    [HttpPut("api/calendars/{id:int}/exceptions/{exceptionId:int}", Name = "UpdateCalendarException")]
    [ProducesResponseType<CalendarExceptionModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateCalendarException(int id, int exceptionId, [FromBody] SaveCalendarExceptionRequest request)
        => ToResponse(await exceptions.UpdateForCalendarAsync(id, exceptionId, request), r => Ok(r.Data));

    [HttpDelete("api/calendars/{id:int}/exceptions/{exceptionId:int}", Name = "DeleteCalendarException")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCalendarException(int id, int exceptionId)
        => ToResponse(await exceptions.DeleteForCalendarAsync(id, exceptionId), _ => NoContent());

    // ---- Italian holidays ---------------------------------------------------------------------------------

    /// <summary>Fixed national holidays plus Easter Sunday and Monday of the year, flagged when already present.</summary>
    [HttpGet("api/calendars/{id:int}/holidays/italy", Name = "PreviewItalianHolidays")]
    [ProducesResponseType<ItalianHolidaysPreviewModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PreviewItalianHolidays(int id, [FromQuery] int year)
        => ToResponse(await exceptions.PreviewItalianHolidaysAsync(id, year), r => Ok(r.Data));

    /// <summary>Creates a Closed exception per selected holiday (already covered days are skipped).</summary>
    [HttpPost("api/calendars/{id:int}/holidays/italy", Name = "ImportItalianHolidays")]
    [ProducesResponseType<ImportItalianHolidaysResultModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ImportItalianHolidays(int id, [FromBody] ImportItalianHolidaysRequest request)
        => ToResponse(await exceptions.ImportItalianHolidaysAsync(id, request), r => Ok(r.Data));

    // ---- machine exceptions -------------------------------------------------------------------------------

    [HttpGet("api/equipment/{id:int}/exceptions", Name = "GetEquipmentExceptions")]
    [ProducesResponseType<List<CalendarExceptionModel>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEquipmentExceptions(int id, [FromQuery] int? year = null)
        => ToResponse(await exceptions.ListForEquipmentAsync(id, year), r => Ok(r.Data));

    [HttpPost("api/equipment/{id:int}/exceptions", Name = "CreateEquipmentException")]
    [ProducesResponseType<CalendarExceptionModel>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateEquipmentException(int id, [FromBody] SaveCalendarExceptionRequest request)
        => ToResponse(await exceptions.CreateForEquipmentAsync(id, request), r => Created($"api/equipment/{id}/exceptions/{r.Data!.Id}", r.Data));

    [HttpPut("api/equipment/{id:int}/exceptions/{exceptionId:int}", Name = "UpdateEquipmentException")]
    [ProducesResponseType<CalendarExceptionModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateEquipmentException(int id, int exceptionId, [FromBody] SaveCalendarExceptionRequest request)
        => ToResponse(await exceptions.UpdateForEquipmentAsync(id, exceptionId, request), r => Ok(r.Data));

    [HttpDelete("api/equipment/{id:int}/exceptions/{exceptionId:int}", Name = "DeleteEquipmentException")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEquipmentException(int id, int exceptionId)
        => ToResponse(await exceptions.DeleteForEquipmentAsync(id, exceptionId), _ => NoContent());

    private IActionResult ToResponse<T>(CalendarResult<T> result, Func<CalendarResult<T>, IActionResult> success)
    {
        if (result.IsSuccessful) return success(result);
        var detail = string.Join("; ", result.Errors.Distinct());
        return result.Failure switch
        {
            CalendarFailure.NotFound => Problem(detail, statusCode: StatusCodes.Status404NotFound),
            CalendarFailure.Conflict => Problem(detail, statusCode: StatusCodes.Status409Conflict),
            CalendarFailure.Invalid => Problem(detail, statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => Problem(detail, statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
