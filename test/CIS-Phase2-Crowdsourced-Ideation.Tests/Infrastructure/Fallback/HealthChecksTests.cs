using System.Data;
using System.Data.Common;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.HealthChecks;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Infrastructure.Fallback;

public sealed class HealthChecksTests
{
    [Fact]
    public async Task MySqlHealthCheck_WhenQuerySucceeds_ReturnsHealthy()
    {
        var factory = new Mock<IMySqlConnectionFactory>();
        factory.Setup(f => f.Create()).Returns(new FakeDbConnection(shouldFail: false));

        var sut = new MySqlHealthCheck(factory.Object, NullLogger<MySqlHealthCheck>.Instance);
        var res = await sut.CheckHealthAsync(new HealthCheckContext());

        res.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task MySqlHealthCheck_WhenQueryFails_ReturnsUnhealthy()
    {
        var factory = new Mock<IMySqlConnectionFactory>();
        factory.Setup(f => f.Create()).Returns(new FakeDbConnection(shouldFail: true));

        var sut = new MySqlHealthCheck(factory.Object, NullLogger<MySqlHealthCheck>.Instance);
        var res = await sut.CheckHealthAsync(new HealthCheckContext());

        res.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task MongoHealthCheck_WhenPingSucceeds_ReturnsHealthy()
    {
        var client = new Mock<IMongoClient>();
        var db = new Mock<IMongoDatabase>();
        db.Setup(d => d.RunCommandAsync<BsonDocument>(
                It.IsAny<Command<BsonDocument>>(),
                It.IsAny<ReadPreference?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BsonDocument("ok", 1));
        client.Setup(c => c.GetDatabase("admin", null)).Returns(db.Object);

        var factory = new Mock<IMongoClientFactory>();
        factory.Setup(f => f.Create()).Returns(client.Object);

        var sut = new MongoDbHealthCheck(factory.Object, NullLogger<MongoDbHealthCheck>.Instance);
        var res = await sut.CheckHealthAsync(new HealthCheckContext());

        res.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task MongoHealthCheck_WhenPingThrows_ReturnsUnhealthy()
    {
        var client = new Mock<IMongoClient>();
        var db = new Mock<IMongoDatabase>();
        db.Setup(d => d.RunCommandAsync<BsonDocument>(
                It.IsAny<Command<BsonDocument>>(),
                It.IsAny<ReadPreference?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("boom"));
        client.Setup(c => c.GetDatabase("admin", null)).Returns(db.Object);

        var factory = new Mock<IMongoClientFactory>();
        factory.Setup(f => f.Create()).Returns(client.Object);

        var sut = new MongoDbHealthCheck(factory.Object, NullLogger<MongoDbHealthCheck>.Instance);
        var res = await sut.CheckHealthAsync(new HealthCheckContext());

        res.Status.Should().Be(HealthStatus.Unhealthy);
    }

    private sealed class FakeDbConnection(bool shouldFail) : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;

        public override string? ConnectionString { get; set; } = "fake";
        public override string Database => "fake";
        public override string DataSource => "fake";
        public override string ServerVersion => "0";
        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() => _state = ConnectionState.Closed;
        public override void Open()
        {
            _state = ConnectionState.Open;
        }

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            _state = ConnectionState.Open;
            return Task.CompletedTask;
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => new FakeDbCommand(shouldFail);
    }

    private sealed class FakeDbCommand(bool shouldFail) : DbCommand
    {
        public override string? CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection { get; } = new FakeDbParameterCollection();
        protected override DbTransaction? DbTransaction { get; set; }
        public override bool DesignTimeVisible { get; set; }

        public override void Cancel() { }
        public override int ExecuteNonQuery() => throw new NotSupportedException();
        public override object ExecuteScalar() => shouldFail ? throw new InvalidOperationException("fail") : 1;
        public override void Prepare() { }

        protected override DbParameter CreateDbParameter() => throw new NotSupportedException();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
            throw new NotSupportedException();

        public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) =>
            Task.FromResult<object?>(shouldFail ? throw new InvalidOperationException("fail") : 1);
    }

    private sealed class FakeDbParameterCollection : DbParameterCollection
    {
        public override int Count => 0;
        public override object SyncRoot { get; } = new();
        public override int Add(object value) => throw new NotSupportedException();
        public override void AddRange(Array values) => throw new NotSupportedException();
        public override void Clear() { }
        public override bool Contains(object value) => false;
        public override bool Contains(string value) => false;
        public override void CopyTo(Array array, int index) { }
        public override System.Collections.IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
        public override int IndexOf(object value) => -1;
        public override int IndexOf(string parameterName) => -1;
        public override void Insert(int index, object value) => throw new NotSupportedException();
        public override void Remove(object value) { }
        public override void RemoveAt(int index) { }
        public override void RemoveAt(string parameterName) { }
        protected override DbParameter GetParameter(int index) => throw new NotSupportedException();
        protected override DbParameter GetParameter(string parameterName) => throw new NotSupportedException();
        protected override void SetParameter(int index, DbParameter value) => throw new NotSupportedException();
        protected override void SetParameter(string parameterName, DbParameter value) => throw new NotSupportedException();
    }
}
