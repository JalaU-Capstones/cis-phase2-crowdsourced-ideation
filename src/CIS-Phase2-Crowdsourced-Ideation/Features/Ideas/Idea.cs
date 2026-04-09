using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;

public class Idea
{
    public Guid Id { get; set; }
    public string TopicId { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }

    // Legacy schema compatibility:
    // The database stores idea content in a single TEXT column (`ideas.content`).
    // To preserve the public API contract (Title/Description/IsWinning), we serialize those fields into Content as JSON.
    private string _content = string.Empty;
    private string _title = string.Empty;
    private string _description = string.Empty;
    private bool _isWinning;
    private bool _suppressSync;

    public string Content
    {
        get => _content;
        set
        {
            _content = value ?? string.Empty;
            HydrateFromContent(_content);
        }
    }

    [NotMapped]
    public string Title
    {
        get => _title;
        set
        {
            _title = value ?? string.Empty;
            SyncContent();
        }
    }

    [NotMapped]
    public string Description
    {
        get => _description;
        set
        {
            _description = value ?? string.Empty;
            SyncContent();
        }
    }

    [NotMapped]
    public bool IsWinning
    {
        get => _isWinning;
        set
        {
            _isWinning = value;
            SyncContent();
        }
    }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Topic Topic { get; set; } = null!;
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private void SyncContent()
    {
        if (_suppressSync)
            return;

        _suppressSync = true;
        try
        {
            _content = JsonSerializer.Serialize(new IdeaContent(_title, _description, _isWinning), JsonOptions);
        }
        finally
        {
            _suppressSync = false;
        }
    }

    private void HydrateFromContent(string content)
    {
        if (_suppressSync)
            return;

        // Best-effort: if content isn't our JSON format (or is empty), keep fields as-is.
        if (string.IsNullOrWhiteSpace(content))
            return;

        try
        {
            var parsed = JsonSerializer.Deserialize<IdeaContent>(content, JsonOptions);
            if (parsed is null)
                return;

            _suppressSync = true;
            try
            {
                _title = parsed.Title ?? string.Empty;
                _description = parsed.Description ?? string.Empty;
                _isWinning = parsed.IsWinning;
            }
            finally
            {
                _suppressSync = false;
            }
        }
        catch (JsonException)
        {
            // Ignore malformed/non-JSON content.
        }
    }

    private sealed record IdeaContent(string Title, string Description, bool IsWinning);
}

public class Vote
{
    public Guid Id { get; set; }
    public Guid IdeaId { get; set; }
    public Guid UserId { get; set; }

    // Not part of the legacy `votes` table schema; kept for API/tests compatibility.
    [NotMapped]
    public bool IsUpvote { get; set; }

    // Not part of the legacy `votes` table schema; kept for API/tests compatibility.
    [NotMapped]
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Idea Idea { get; set; } = null!;
}
