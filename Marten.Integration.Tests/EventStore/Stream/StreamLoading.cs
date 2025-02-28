using FluentAssertions;
using Marten.Integration.Tests.TestsInfrastructure;
using Xunit;

namespace Marten.Integration.Tests.EventStore.Stream;

public class StreamLoading: MartenTest
{
    public record IssueCreated(
        Guid IssueId,
        string Description
    );

    public record IssueUpdated(
        Guid IssueId,
        string Description
    );

    private readonly Guid issueId = Guid.NewGuid();

    private Guid GetExistingStreamId()
    {
        var @event = new IssueCreated(issueId, "Description");
        var streamId = EventStore.StartStream(@event).Id;
        Session.SaveChanges();

        return streamId;
    }

    [Fact]
    public void GivenExistingStream_WithOneEventWhenStreamIsLoaded_ThenItLoadsOneEvent()
    {
        //Given
        var streamId = GetExistingStreamId();

        //When
        var events = EventStore.FetchStream(streamId);

        //Then
        events.Count.Should().Be(1);
        events.First().Version.Should().Be(1);
    }

    [Fact]
    public void GivenExistingStreamWithOneEvent_WhenEventIsAppended_ThenItLoadsTwoEvents()
    {
        //Given
        var streamId = GetExistingStreamId();

        //When
        EventStore.Append(streamId, new IssueUpdated(issueId, "New Description"));
        Session.SaveChanges();

        //Then
        var events = EventStore.FetchStream(streamId);

        events.Count.Should().Be(2);
        events.Last().Version.Should().Be(2);
    }

    [Fact]
    public void GivenExistingStreamWithOneEvent_WhenStreamIsLoadedByEventType_ThenItLoadsOneEvent()
    {
        //Given
        var streamId = GetExistingStreamId();
        var eventId = EventStore.FetchStream(streamId).Single().Id;

        //When
        var @event = EventStore.Load<IssueCreated>(eventId);

        //Then
        @event.Should().NotBeNull();
        @event!.Id.Should().Be(eventId);
    }

    [Fact]
    public async Task GivenExistingStreamWithMultipleEvents_WhenEventsAreQueriedOrderedDescending_ThenLastEventIsLoaded()
    {
        var streamId = Guid.NewGuid();
        Session.Events.Append(streamId,
            new IssueCreated(streamId, "Description"),
            new IssueUpdated(streamId, "Description"),
            new IssueUpdated(streamId, "The Last One")
        );
        await Session.SaveChangesAsync();

        var lastEvent = Session.Events.QueryAllRawEvents()
            .Where(e => e.StreamId == streamId)
            .OrderByDescending(e => e.Version)
            .FirstOrDefault();

        lastEvent.Should().NotBeNull();
        lastEvent!.Data.Should().BeOfType<IssueUpdated>();

        var lastUpdatedEvent = (IssueUpdated)lastEvent.Data;
        lastUpdatedEvent.IssueId.Should().Be(streamId);
        lastUpdatedEvent.Description.Should().Be("The Last One");
    }
}
