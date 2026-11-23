namespace Waterly.Api.DTOs.Users;

public class UserResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string TimeZone { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
}
