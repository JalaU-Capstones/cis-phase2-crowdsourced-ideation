namespace CIS.Phase2.CrowdsourcedIdeation.Features.Shared;

/// <summary>
/// Represents a HATEOAS hypermedia link following the project link format.
/// </summary>
/// <param name="Href">Relative URL of the linked resource (e.g. "api/topics/abc-123").</param>
/// <param name="Method">HTTP method (GET, POST, PUT, DELETE).</param>
/// <param name="Rel">Relation name describing the link's purpose (e.g. "self", "ideas", "vote").</param>
public record LinkDto(string Href, string Method, string Rel);