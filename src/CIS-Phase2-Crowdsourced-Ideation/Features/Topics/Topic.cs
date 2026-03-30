namespace CIS.Phase2.CrowdsourcedIdeation.Features.Topics;

public sealed class Topic
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TopicStatus Status { get; set; } = TopicStatus.OPEN;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum TopicStatus { OPEN, CLOSED }