using System.Text.Json.Serialization;
using CIS.Phase2.CrowdsourcedIdeation.Features.Shared;

namespace CIS_Phase2_Crowdsourced_Ideation.Features.Votes;

public sealed record CastVoteRequest(Guid IdeaId);

public sealed record UpdateVoteRequest(Guid IdeaId);

/// <summary>
/// Vote response shape required by US 2.2:
/// - vote id
/// - idea id + title
/// - topic id + title
/// </summary>
public sealed record VoteResponse(
    Guid Id,
    Guid IdeaId,
    string IdeaTitle,
    string TopicId,
    string TopicTitle
)
{
    /// <summary>
    /// HATEOAS hypermedia links for client navigation (US 3.2).
    /// Includes self (votes for the idea), idea, remove.
    /// </summary>
    [JsonPropertyName("_links")]
    public IReadOnlyList<LinkDto>? Links { get; init; }
}

public sealed record ErrorResponse(string Message, string? ErrorCode = null);