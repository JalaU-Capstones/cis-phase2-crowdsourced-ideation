namespace CIS.Phase2.CrowdsourcedIdeation.Features.Shared;

/// <summary>
/// Centralized builder for HATEOAS link sets (US 3.2).
/// Provides consistent link generation for Topics, Ideas, and Votes.
/// </summary>
public static class HateoasBuilder
{
    /// <summary>
    /// Builds the HATEOAS link set for a Topic resource.
    /// AC-1 / AC-2 / AC-3 / AC-4: self, ideas, winner (only when CLOSED), update, delete.
    /// </summary>
    public static IReadOnlyList<LinkDto> ForTopic(string topicId, string status, string version = "v1")
    {
        var links = new List<LinkDto>
        {
            new($"api/{version}/topics/{topicId}", "GET",    "self"),
            new($"api/{version}/ideas/topic/{topicId}", "GET", "ideas"),
            new($"api/{version}/topics/{topicId}", "PUT",    "update"),
            new($"api/{version}/topics/{topicId}", "DELETE", "delete"),
        };

        // winner link only when topic is CLOSED (AC-1)
        if (string.Equals(status, "CLOSED", StringComparison.OrdinalIgnoreCase))
        {
            links.Add(new($"api/{version}/topics/{topicId}", "GET", "winner"));
        }

        return links.AsReadOnly();
    }

    /// <summary>
    /// Builds the HATEOAS link set for an Idea resource.
    /// AC-5 / AC-6 / AC-7: self, topic, votes, vote (only when topic OPEN), update, delete.
    /// </summary>
    public static IReadOnlyList<LinkDto> ForIdea(Guid ideaId, string topicId, bool topicIsOpen, string version = "v1")
    {
        var links = new List<LinkDto>
        {
            new($"api/{version}/ideas/{ideaId}",          "GET",    "self"),
            new($"api/{version}/topics/{topicId}",         "GET",    "topic"),
            new($"api/{version}/votes/idea/{ideaId}",      "GET",    "votes"),
            new($"api/{version}/ideas/{ideaId}",           "PUT",    "update"),
            new($"api/{version}/ideas/{ideaId}",           "DELETE", "delete"),
        };

        // vote link only when topic is OPEN (AC-5)
        if (topicIsOpen)
        {
            links.Add(new($"api/{version}/votes", "POST", "vote"));
        }

        return links.AsReadOnly();
    }

    /// <summary>
    /// Builds the HATEOAS link set for a Vote resource.
    /// AC-8 / AC-9: self (votes for the idea), idea, remove.
    /// </summary>
    public static IReadOnlyList<LinkDto> ForVote(Guid voteId, Guid ideaId, string version = "v1")
    {
        return new List<LinkDto>
        {
            new($"api/{version}/votes/idea/{ideaId}", "GET",    "self"),
            new($"api/{version}/ideas/{ideaId}",      "GET",    "idea"),
            new($"api/{version}/votes/{voteId}",      "DELETE", "remove"),
        }.AsReadOnly();
    }
}