namespace CIS_Phase2_Crowdsourced_Ideation.Features.Statistics;

public sealed record TopTopicDto(
    string TopicId,
    string TopicTitle,
    string Status,
    int IdeasCount,
    int VotesCount
);

public sealed record MostVotedIdeaDto(
    Guid IdeaId,
    string IdeaTitle,
    string TopicId,
    string TopicTitle,
    int VotesCount
);

public sealed record IdeaBriefDto(
    Guid IdeaId,
    string IdeaTitle,
    int VotesCount
);

public sealed record TopicSummaryDto(
    string TopicId,
    string TopicTitle,
    string Status,
    int IdeasCount,
    int VotesCount,
    IdeaBriefDto? WinningIdea,
    IdeaBriefDto? MostVotedIdea
);

public sealed record ErrorResponse(string Message);

