using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using MongoDB.Bson.Serialization.Attributes;

namespace CIS_Phase2_Crowdsourced_Ideation.Features.Votes;

/// <summary>
/// Represents a vote cast by a user for a specific idea.
///
/// Legacy schema note (init.sql):
/// - votes.id, votes.idea_id, votes.user_id are stored as VARCHAR(36)
/// - there is no created_at column
/// </summary>
public sealed class Vote
{
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public Guid IdeaId { get; set; }
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public Guid UserId { get; set; }

    public Idea Idea { get; set; } = null!;
}
