using System.ComponentModel.DataAnnotations;

namespace Waterly.Api.DTOs.Statistics;

// Without parameters the last 30 days are returned. With year + month that calendar month is returned.
public class MonthlyStatisticsQuery
{
    [Range(2000, 2100)]
    public int? Year { get; set; }

    [Range(1, 12)]
    public int? Month { get; set; }
}
