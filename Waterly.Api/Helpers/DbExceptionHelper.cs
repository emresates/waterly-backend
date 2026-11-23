using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Waterly.Api.Helpers;

public static class DbExceptionHelper
{
    // PostgreSQL error 23505 = unique_violation. Happens when two requests insert the same unique value at the same time.
    public static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
