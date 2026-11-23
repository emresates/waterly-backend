namespace Waterly.Api.Models;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    // SHA-256 hash of the token. The plain token is only ever sent to the client.
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    // Set when this token was rotated. Seeing a rotated token again means it was stolen or replayed.
    public Guid? ReplacedByTokenId { get; set; }

    public User User { get; set; } = null!;

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
