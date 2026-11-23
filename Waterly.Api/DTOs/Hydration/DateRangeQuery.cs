using System.ComponentModel.DataAnnotations;

namespace Waterly.Api.DTOs.Hydration;

// Query string: ?from=2026-11-01&to=2026-11-23&page=1&pageSize=20
public class DateRangeQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "page en az 1 olmalıdır.")]
    public int Page { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "pageSize 1-100 arasında olmalıdır.")]
    public int PageSize { get; set; } = 20;
}
