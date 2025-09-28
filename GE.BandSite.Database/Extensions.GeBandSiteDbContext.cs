using Microsoft.EntityFrameworkCore;

namespace GE.BandSite.Database;

public static partial class Extensions
{
    public static async Task<(User? User, string? ErrorMessage)> GetUserByEmailAsync(this GeBandSiteDbContext dbContext, string email, CancellationToken cancellationToken = default)
    {
        string normalizedEmail = email.ToLowerInvariant().Trim();

        var user = await dbContext.Users
            .AsSplitQuery()
            .Where(x => EF.Functions.ILike(x.Email, normalizedEmail))
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            return (null, "User doesn't exist.");
        }

        return (user, null);
    }
}
