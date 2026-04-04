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
/// Detailed response data for a topic.
/// </summary>
/// <param name="Id">The topic's unique identifier.</param>
/// <param name="Title">The topic's title.</param>
/// <param name="Description">The topic's description.</param>
/// <param name="Status">The topic's current status string.</param>
/// <param name="OwnerId">The topic's creator/owner identifier.</param>
/// <param name="CreatedAt">When the topic was created (UTC).</param>
/// <param name="UpdatedAt">When the topic was last updated (UTC).</param>
public sealed record TopicResponse(
    string Id,
    string Title,
    string? Description,
    string Status,
    Guid OwnerId,
    DateTime CreatedAt,
    DateTime UpdatedAt);