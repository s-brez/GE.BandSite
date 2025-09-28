using GE.BandSite.Database.Configuration;
using NodaTime;

namespace GE.BandSite.Database;

public static partial class Constants
{
    private static IDictionary<string, SeedUserConfiguration>? SystemUserConfiguration { get; set; }

    public static void SetSystemUserConfiguration(IDictionary<string, SeedUserConfiguration>? configuration)
    {
        SystemUserConfiguration = configuration;
    }

    public static Guid SystemUser1Id = new("ed61935a-4563-4982-8279-9526f2df9c5b");
    public static Guid SystemUser2Id = new("148c64a5-38d5-480f-b1a3-24dd45f2c962");
    public static Guid SystemUser3Id = new("60407bf6-4c63-44e2-a9fe-d081c206729a");
    public static Guid SystemUser4Id = new("4ae673c7-0f9e-4108-b9eb-bc83634f6909");

    public static User[] SystemUsers
    {
        get
        {
            if (SystemUserConfiguration == null || SystemUserConfiguration.Count == 0)
            {
                return Array.Empty<User>();
            }

            string user1RequiredEmail = "sam@sdbgroup.io";

            var users = new List<User>();

            if (SystemUserConfiguration.TryGetValue(user1RequiredEmail, out var systemUser1Config))
            {
                users.Add(new User()
                {
                    Id = SystemUser1Id,
                    FirstName = "Sam",
                    LastName = "Breznikar",
                    Email = user1RequiredEmail,
                    ExternalPositionDescription = "System user",
                    EmailConfirmedDateTime = SystemClock.Instance.GetCurrentInstant(),
                    IsActive = true,
                    IsLocked = false,
                    PasswordHash = Convert.FromBase64String(systemUser1Config.PasswordHash),
                    Phone = "+61412345678",
                    PhoneConfirmedDateTime = SystemClock.Instance.GetCurrentInstant(),
                    PreviousPasswordHashes = new List<byte[]>(),
                    Salt = Convert.FromBase64String(systemUser1Config.Salt)
                });
            }

            return users.ToArray();
        }
    }
}
