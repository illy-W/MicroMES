using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ProcessSegmentFeature.Models;

public class ProcessSegmentModel
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required decimal SetupMinutes { get; set; }
    public required decimal RunMinutesPerUnit { get; set; }
    public required bool IsActive { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }

    public static ProcessSegmentModel FromEntity(ProcessSegment s) => new()
    {
        Id = s.Id,
        Code = s.Code,
        Name = s.Name,
        Description = s.Description,
        SetupMinutes = s.SetupMinutes,
        RunMinutesPerUnit = s.RunMinutesPerUnit,
        IsActive = s.IsActive,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt
    };
}
