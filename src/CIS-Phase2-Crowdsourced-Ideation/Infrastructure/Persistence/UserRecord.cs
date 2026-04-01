using Microsoft.EntityFrameworkCore;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;


[Index(nameof(Login), IsUnique = true)]
public sealed class UserRecord
{
    public string Id    { get; init; } = default!;
    public string Name  { get; init; } = default!;
    public string Login { get; init; } = default!;
    public string Password { get; init; } = default!; 
}