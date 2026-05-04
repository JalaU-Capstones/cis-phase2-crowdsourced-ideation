using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Infrastructure.Persistence;

public sealed class MongoDbContextTests
{
    [Fact]
    public void Collections_HaveExpectedNames()
    {
        // MongoDB driver does not hit the network for GetCollection; this is safe as a pure unit test.
        var sut = new MongoDbContext("mongodb://localhost:27017", "unit_test_db");

        sut.Topics.CollectionNamespace.CollectionName.Should().Be("topics");
        sut.Ideas.CollectionNamespace.CollectionName.Should().Be("ideas");
        sut.Votes.CollectionNamespace.CollectionName.Should().Be("votes");
        sut.Users.CollectionNamespace.CollectionName.Should().Be("users");
    }
}

