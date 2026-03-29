namespace CIS.Phase2.CrowdsourcedIdeation.Features.Topics;

public sealed record CreateTopicRequest(string Title, string? Description);

public sealed record UpdateTopicRequest(string Title, string? Description, string Status);

public sealed record TopicResponse(
    string Id,
    string Title,
    string? Description,
    string Status,
    string CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt);