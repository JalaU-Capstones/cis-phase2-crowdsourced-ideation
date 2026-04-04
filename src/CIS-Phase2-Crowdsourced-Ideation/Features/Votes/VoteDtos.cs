namespace CIS.Phase2.CrowdsourcedIdeation.Features.Votes;

public record VoteResponse(
    string Id,
    string IdeaId,
    string UserId,
    DateTime CreatedAt
);

public record ErrorResponse(string Message, string? ErrorCode = null);