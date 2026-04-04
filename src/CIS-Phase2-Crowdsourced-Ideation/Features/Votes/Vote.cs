namespace CIS.Phase2.CrowdsourcedIdeation.Features.Votes;

public sealed class Vote
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string IdeaId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}