namespace CIS.Phase2.CrowdsourcedIdeation.Features.Ideas;

public enum IdeaStatus
{
    OPEN,
    IN_REVIEW,
    IMPLEMENTED,
    REJECTED
}

public sealed class Idea
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TopicId { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public IdeaStatus Status { get; set; } = IdeaStatus.OPEN;
    public int VoteCount { get; set; } = 0;
}