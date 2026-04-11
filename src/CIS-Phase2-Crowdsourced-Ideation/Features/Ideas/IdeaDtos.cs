using System;

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
);