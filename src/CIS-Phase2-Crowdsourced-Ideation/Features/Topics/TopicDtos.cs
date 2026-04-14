using System.Text.Json.Serialization;
using CIS.Phase2.CrowdsourcedIdeation.Features.Shared;

namespace CIS.Phase2.CrowdsourcedIdeation.Features.Topics;

/// <summary>
/// Data required to create a new topic.
/// </summary>
/// <param name="Title">The title of the topic.</param>
/// <param name="Description">An optional description.</param>
public sealed record CreateTopicRequest(string Title, string? Description);

/// <summary>
/// Data required to update an existing topic.
/// </summary>
/// <param name="Title">The updated title.</param>
/// <param name="Description">The updated description.</param>
/// <param name="Status">The updated status (OPEN/CLOSED).</param>
public sealed record UpdateTopicRequest(string Title, string? Description, string Status);

/// <summary>
/// Detailed response data for the winning idea of a closed topic.
/// </summary>
/// <param name="Id">The idea's unique identifier.</param>
/// <param name="TopicId">The topic identifier this idea belongs to.</param>
/// <param name="OwnerId">The creator/owner identifier.</param>
/// <param name="Title">The idea title (derived from the legacy <c>ideas.content</c> JSON).</param>
/// <param name="Description">The idea description (derived from the legacy <c>ideas.content</c> JSON).</param>
/// <param name="CreatedAt">When the idea was created (UTC).</param>
/// <param name="UpdatedAt">When the idea was last updated (UTC).</param>
/// <param name="IsWinning">Whether the idea is marked as the winner.</param>
public sealed record WinningIdeaResponse(
    Guid Id,
    string TopicId,
    Guid OwnerId,
    string Title,
    string Description,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsWinning);

/// <summary>
/// Detailed response data for a topic.
/// </summary>
/// <param name="Id">The topic's unique identifier.</param>
/// <param name="Title">The topic's title.</param>
/// <param name="Description">The topic's description.</param>
/// <param name="Status">The topic's current status string.</param>
/// <param name="OwnerId">The topic's creator/owner identifier.</param>
/// <param name="CreatedAt">When the topic was created (UTC).</param>
/// <param name="UpdatedAt">When the topic was last updated (UTC).</param>
/// <param name="WinningIdea">If the topic is CLOSED and a winning idea exists (IsWinning=true), it is returned here.</param>
public sealed record TopicResponse(
    string Id,
    string Title,
    string? Description,
    string Status,
    string OwnerId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    WinningIdeaResponse? WinningIdea)
{
    /// <summary>
    /// HATEOAS hypermedia links for client navigation.
    /// Includes self, ideas, update, delete. Includes winner only when status is CLOSED.
    /// </summary>
    [JsonPropertyName("_links")]
    public IReadOnlyList<LinkDto>? Links { get; init; }
}