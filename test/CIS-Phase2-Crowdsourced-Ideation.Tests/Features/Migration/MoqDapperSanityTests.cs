using Dapper;
using FluentAssertions;
using Moq;
using Moq.Dapper;
using System.Data.Common;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Migration;

public sealed class MoqDapperSanityTests
{
    [Fact]
    public async Task SetupDapperAsync_Works_For_QueryAsync_String()
    {
        var conn = new Mock<DbConnection>();
        conn.SetupDapperAsync(c => c.QueryAsync<int>(It.IsAny<CommandDefinition>()))
            .ReturnsAsync(new[] { 1 });

        var result = await conn.Object.QueryAsync<int>("SELECT 1");
        result.Should().ContainSingle().Which.Should().Be(1);
    }

    [Fact]
    public async Task SetupDapperAsync_CanMatch_CommandTextPredicate()
    {
        var conn = new Mock<DbConnection>();
        conn.SetupDapperAsync(c => c.QueryAsync<string>(
                It.IsAny<CommandDefinition>()))
            .ReturnsAsync(new[] { "u1" });

        var result = await conn.Object.QueryAsync<string>("SELECT DISTINCT owner_id FROM topics");
        result.Should().ContainSingle().Which.Should().Be("u1");
    }
}
