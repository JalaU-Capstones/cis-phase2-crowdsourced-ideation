using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS_Phase2_Crowdsourced_Ideation.Features.Votes;
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
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
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

        if (TryParseIdeaContent(content, out var parsed))
        {
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
    }

    private sealed record IdeaContent(string Title, string Description, bool IsWinning);

    private static bool TryParseIdeaContent(string content, out IdeaContent parsed)
    {
        parsed = default!;

        // Most common case: content is a JSON object.
        try
        {
            var direct = JsonSerializer.Deserialize<IdeaContent>(content, JsonOptions);
            if (direct is not null)
            {
                parsed = direct;
                return true;
            }
        }
        catch (JsonException)
        {
            // Fall through to other formats.
        }

        // Some legacy data may be double-encoded, e.g. "\"{\\\"title\\\":...}\""
        // (a JSON string whose value is the JSON object).
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind == JsonValueKind.String)
            {
                var inner = doc.RootElement.GetString();
                if (!string.IsNullOrWhiteSpace(inner))
                {
                    var innerParsed = JsonSerializer.Deserialize<IdeaContent>(inner, JsonOptions);
                    if (innerParsed is not null)
                    {
                        parsed = innerParsed;
                        return true;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Ignore malformed/non-JSON content.
        }

        return false;
    }
}
