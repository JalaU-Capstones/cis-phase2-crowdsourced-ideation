namespace CIS.Phase2.CrowdsourcedIdeation.Features.Topics;

/// <summary>
/// Represents a topic for discussion and ideation.
/// </summary>
public sealed class Topic
{
    /// <summary>
    /// Unique identifier for the topic (UUID).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The title of the topic.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// An optional description of the topic.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The current status of the topic (OPEN, CLOSED).
    /// </summary>
    public TopicStatus Status { get; set; } = TopicStatus.OPEN;

    /// <summary>
    /// The identifier of the user who owns/created the topic.
    /// </summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>
    /// The date and time when the topic was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// The date and time when the topic was last updated (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Defines the possible states of a topic.
/// </summary>
public enum TopicStatus 
{ 
    /// <summary>
    /// The topic is open for new ideas and voting.
    /// </summary>
    OPEN, 

    /// <summary>
    /// The topic is closed for further activity.
    /// </summary>
    CLOSED 
}