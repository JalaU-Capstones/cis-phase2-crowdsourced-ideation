using Microsoft.EntityFrameworkCore;
using MongoDB.Bson.Serialization.Attributes;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;


[Index(nameof(Login), IsUnique = true)]
[BsonIgnoreExtraElements]
public sealed class UserRecord
{
    public string Id    { get; init; } = default!;
    public string Name  { get; init; } = default!;
    public string Login { get; init; } = default!;
    public string Password { get; init; } = default!; 
}
