using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SmartCampus.Infrastructure;

public sealed class EmailConfirmationOptions : DataProtectionTokenProviderOptions
{
    public EmailConfirmationOptions() { Name = "SmartCampusEmailConfirmation"; TokenLifespan = TimeSpan.FromHours(24); }
}

public sealed class EmailConfirmationProvider(IDataProtectionProvider provider, IOptions<EmailConfirmationOptions> options,
    ILogger<DataProtectorTokenProvider<CampusUser>> logger) : DataProtectorTokenProvider<CampusUser>(provider, options, logger);
