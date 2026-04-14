using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using Microsoft.EntityFrameworkCore;

namespace CIS_Phase2_Crowdsourced_Ideation.Features.Statistics;

public interface IStatisticsService
{
    Task<IReadOnlyList<TopTopicDto>> GetTopTopicsAsync(int limit, int offset);
    Task<IReadOnlyList<MostVotedIdeaDto>> GetMostVotedIdeasAsync(int limit, int offset);
    Task<TopicSummaryDto?> GetTopicSummaryAsync(string topicId);
}

public sealed class StatisticsService(AppDbContext db) : IStatisticsService
{
    public async Task<IReadOnlyList<TopTopicDto>> GetTopTopicsAsync(int limit, int offset)
    {
        // Query counts in the database when possible. The final ordering/paging is done in-memory
        // because different providers (MySql/InMemory) behave differently for some grouping scenarios.
        var topics = await db.Topics
            .AsNoTracking()
            .Select(t => new { t.Id, t.Title, t.Status })
            .ToListAsync();

        if (topics.Count == 0)
            return Array.Empty<TopTopicDto>();

        var ideaCounts = await db.Ideas
            .AsNoTracking()
            .GroupBy(i => i.TopicId)
            .Select(g => new { TopicId = g.Key, Count = g.Count() })
            .ToListAsync();

        var voteCountsByTopic = await (
                from v in db.Votes.AsNoTracking()
                join i in db.Ideas.AsNoTracking() on v.IdeaId equals i.Id
                group v by i.TopicId
                into g
                select new { TopicId = g.Key, Count = g.Count() }
            )
            .ToListAsync();

        var ideaCountMap = ideaCounts.ToDictionary(x => x.TopicId, x => x.Count);
        var voteCountMap = voteCountsByTopic.ToDictionary(x => x.TopicId, x => x.Count);

        var result = topics
            .Select(t => new TopTopicDto(
                t.Id,
                t.Title,
                t.Status.ToString(),
                ideaCountMap.GetValueOrDefault(t.Id, 0),
                voteCountMap.GetValueOrDefault(t.Id, 0)))
            .OrderByDescending(t => t.VotesCount)
            .ThenByDescending(t => t.IdeasCount)
            .ThenBy(t => t.TopicTitle)
            .Skip(offset)
            .Take(limit)
            .ToList();

        return result;
    }

    public async Task<IReadOnlyList<MostVotedIdeaDto>> GetMostVotedIdeasAsync(int limit, int offset)
    {
        // We must materialize ideas to get Title (it's derived from legacy ideas.content JSON).
        var ideas = await db.Ideas
            .AsNoTracking()
            .Include(i => i.Topic)
            .ToListAsync();

        if (ideas.Count == 0)
            return Array.Empty<MostVotedIdeaDto>();

        var voteCounts = await db.Votes
            .AsNoTracking()
            .GroupBy(v => v.IdeaId)
            .Select(g => new { IdeaId = g.Key, Count = g.Count() })
            .ToListAsync();

        var voteCountMap = voteCounts.ToDictionary(x => x.IdeaId, x => x.Count);

        var result = ideas
            .Select(i => new MostVotedIdeaDto(
                i.Id,
                i.Title,
                i.TopicId,
                i.Topic.Title,
                voteCountMap.GetValueOrDefault(i.Id, 0)))
            .OrderByDescending(i => i.VotesCount)
            .ThenBy(i => i.IdeaTitle)
            .Skip(offset)
            .Take(limit)
            .ToList();

        return result;
    }

    public async Task<TopicSummaryDto?> GetTopicSummaryAsync(string topicId)
    {
        var topic = await db.Topics
            .AsNoTracking()
            .Where(t => t.Id == topicId)
            .Select(t => new { t.Id, t.Title, t.Status })
            .FirstOrDefaultAsync();

        if (topic is null)
            return null;

        var ideas = await db.Ideas
            .AsNoTracking()
            .Where(i => i.TopicId == topicId)
            .ToListAsync();

        var ideasCount = ideas.Count;
        if (ideasCount == 0)
        {
            return new TopicSummaryDto(
                topic.Id,
                topic.Title,
                topic.Status.ToString(),
                0,
                0,
                WinningIdea: null,
                MostVotedIdea: null);
        }

        var ideaIds = ideas.Select(i => i.Id).ToList();

        var voteCounts = await db.Votes
            .AsNoTracking()
            .Where(v => ideaIds.Contains(v.IdeaId))
            .GroupBy(v => v.IdeaId)
            .Select(g => new { IdeaId = g.Key, Count = g.Count() })
            .ToListAsync();

        var voteCountMap = voteCounts.ToDictionary(x => x.IdeaId, x => x.Count);
        var totalVotes = voteCounts.Sum(x => x.Count);

        var winning = ideas.FirstOrDefault(i => i.IsWinning);
        IdeaBriefDto? winningDto = null;
        if (winning is not null)
        {
            winningDto = new IdeaBriefDto(winning.Id, winning.Title, voteCountMap.GetValueOrDefault(winning.Id, 0));
        }

        var mostVoted = ideas
            .OrderByDescending(i => voteCountMap.GetValueOrDefault(i.Id, 0))
            .ThenBy(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .First();

        var mostVotedDto = new IdeaBriefDto(mostVoted.Id, mostVoted.Title, voteCountMap.GetValueOrDefault(mostVoted.Id, 0));

        return new TopicSummaryDto(
            topic.Id,
            topic.Title,
            topic.Status.ToString(),
            ideasCount,
            totalVotes,
            winningDto,
            mostVotedDto);
    }
}

