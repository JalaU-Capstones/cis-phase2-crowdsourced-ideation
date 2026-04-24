using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;
using CIS.Phase2.CrowdsourcedIdeation.Features.Shared;

namespace CIS_Phase2_Crowdsourced_Ideation.Features.Statistics;

public interface IStatisticsService
{
    Task<IReadOnlyList<TopTopicDto>> GetTopTopicsAsync(int limit, int offset);
    Task<IReadOnlyList<MostVotedIdeaDto>> GetMostVotedIdeasAsync(int limit, int offset);
    Task<TopicSummaryDto?> GetTopicSummaryAsync(string topicId);
}

public sealed class StatisticsService(IRepositoryAdapter adapter, string version = "v1") : IStatisticsService
{
    public async Task<IReadOnlyList<TopTopicDto>> GetTopTopicsAsync(int limit, int offset)
    {
        var topics = await adapter.Topics.GetAllAsync();
        if (!topics.Any())
            return Array.Empty<TopTopicDto>();

        var results = new List<TopTopicDto>();
        foreach (var t in topics)
        {
            var ideas = await adapter.Ideas.GetByTopicIdAsync(t.Id);
            int totalVotes = 0;
            foreach (var idea in ideas)
            {
                totalVotes += await adapter.Votes.CountByIdeaIdAsync(idea.Id);
            }
            results.Add(new TopTopicDto(t.Id, t.Title, t.Status.ToString(), ideas.Count(), totalVotes)
            {
                Links = HateoasBuilder.ForTopTopic(t.Id, version)
            });
        }

        return results
            .OrderByDescending(t => t.VotesCount)
            .ThenByDescending(t => t.IdeasCount)
            .ThenBy(t => t.TopicTitle)
            .Skip(offset)
            .Take(limit)
            .ToList();
    }

    public async Task<IReadOnlyList<MostVotedIdeaDto>> GetMostVotedIdeasAsync(int limit, int offset)
    {
        var ideas = await adapter.Ideas.GetAllAsync();
        if (!ideas.Any())
            return Array.Empty<MostVotedIdeaDto>();

        var results = new List<MostVotedIdeaDto>();
        foreach (var i in ideas)
        {
            var topic = await adapter.Topics.GetByIdAsync(i.TopicId);
            var voteCount = await adapter.Votes.CountByIdeaIdAsync(i.Id);
            results.Add(new MostVotedIdeaDto(i.Id, i.Title, i.TopicId, topic?.Title ?? "N/A", voteCount)
            {
                Links = HateoasBuilder.ForMostVotedIdea(i.Id, i.TopicId, version)
            });
        }

        return results
            .OrderByDescending(i => i.VotesCount)
            .ThenBy(i => i.IdeaTitle)
            .Skip(offset)
            .Take(limit)
            .ToList();
    }

    public async Task<TopicSummaryDto?> GetTopicSummaryAsync(string topicId)
    {
        var topic = await adapter.Topics.GetByIdAsync(topicId);
        if (topic is null)
            return null;

        var ideas = await adapter.Ideas.GetByTopicIdAsync(topicId);
        var ideasCount = ideas.Count();
        
        if (ideasCount == 0)
        {
            return new TopicSummaryDto(
                topic.Id,
                topic.Title,
                topic.Status.ToString(),
                0,
                0,
                WinningIdea: null,
                MostVotedIdea: null)
            {
                Links = HateoasBuilder.ForTopicSummary(topicId, version)
            };
        }

        int totalVotes = 0;
        var ideaVotes = new Dictionary<Guid, int>();
        foreach (var idea in ideas)
        {
            var count = await adapter.Votes.CountByIdeaIdAsync(idea.Id);
            ideaVotes[idea.Id] = count;
            totalVotes += count;
        }

        var winning = ideas.FirstOrDefault(i => i.IsWinning);
        IdeaBriefDto? winningDto = winning != null 
            ? new IdeaBriefDto(winning.Id, winning.Title, ideaVotes.GetValueOrDefault(winning.Id, 0))
            : null;

        var mostVoted = ideas
            .OrderByDescending(i => ideaVotes.GetValueOrDefault(i.Id, 0))
            .ThenBy(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .First();

        var mostVotedDto = new IdeaBriefDto(mostVoted.Id, mostVoted.Title, ideaVotes.GetValueOrDefault(mostVoted.Id, 0));

        return new TopicSummaryDto(
            topic.Id,
            topic.Title,
            topic.Status.ToString(),
            ideasCount,
            totalVotes,
            winningDto,
            mostVotedDto)
        {
            Links = HateoasBuilder.ForTopicSummary(topicId, version)
        };
    }
}
