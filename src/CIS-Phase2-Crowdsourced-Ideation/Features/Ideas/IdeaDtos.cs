using System.Text.Json.Serialization;
using CIS.Phase2.CrowdsourcedIdeation.Features.Shared;

namespace CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;

public record CreateIdeaRequest(string TopicId, string Title, string Description);
public record UpdateIdeaRequest(string Title, string Description);

public record IdeaResponse(
    Guid Id,
    string TopicId,
    Guid OwnerId,
    string Title,
    string Description,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsWinning
)
{
    /// <summary>
    /// HATEOAS hypermedia links for client navigation (US 3.2).
    /// Includes self, topic, votes, update, delete. Includes vote only when topic is OPEN.
    /// </summary>
    [JsonPropertyName("_links")]
    public IReadOnlyList<LinkDto>? Links { get; init; }
}