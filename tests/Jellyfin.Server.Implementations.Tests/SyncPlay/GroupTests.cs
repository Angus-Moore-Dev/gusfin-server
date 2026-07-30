using System;
using System.Collections.Generic;
using System.Threading;
using Emby.Server.Implementations.SyncPlay;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Controller.SyncPlay.Requests;
using MediaBrowser.Model.SyncPlay;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.SyncPlay;

public class GroupTests
{
    public GroupTests()
    {
        var mockLogger = new Mock<ILogger<Emby.Server.Implementations.SyncPlay.Group>>();
        MockLoggerFactory = new Mock<ILoggerFactory>();
        MockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

        MockUserManager = new Mock<IUserManager>();
        MockSessionManager = new Mock<ISessionManager>();
        MockLibraryManager = new Mock<ILibraryManager>();
        MockItem = new Mock<BaseItem>();
        MockItem.Setup(i => i.IsVisibleStandalone(It.IsAny<User>())).Returns(true);
    }

    private Mock<ILoggerFactory> MockLoggerFactory { get; }

    private Mock<IUserManager> MockUserManager { get; }

    private Mock<ISessionManager> MockSessionManager { get; }

    private Mock<ILibraryManager> MockLibraryManager { get; }

    private Mock<BaseItem> MockItem { get; }

    [Fact]
    public void HasAccessToPlayQueue_ReturnsTrue_WhenItemsAreVisible()
    {
        MockLibraryManager.Setup(m => m.GetItemById(It.IsAny<Guid>())).Returns(MockItem.Object);

        var group = new Emby.Server.Implementations.SyncPlay.Group(MockLoggerFactory.Object, MockUserManager.Object, MockSessionManager.Object, MockLibraryManager.Object);
        var itemId = Guid.NewGuid();
        var playlist = new List<Guid> { itemId };
        group.PlayQueue.Reset();
        group.PlayQueue.SetPlaylist(playlist);

        Assert.Single(group.PlayQueue.GetPlaylist());
        Assert.Equal(itemId, group.PlayQueue.GetPlaylist()[0].ItemId);

        var user = new User("test-user", "auth-provider", "pwdreset-provider");
        var result = group.HasAccessToPlayQueue(user);

        Assert.True(result);
    }

    [Fact]
    public void HasAccessToPlayQueue_ReturnsFalse_WhenLibraryReturnsNullForItem()
    {
        MockLibraryManager.Setup(m => m.GetItemById(It.IsAny<Guid>())).Returns((BaseItem?)null);

        Assert.Null(MockLibraryManager.Object.GetItemById(Guid.NewGuid()));

        var group = new Emby.Server.Implementations.SyncPlay.Group(MockLoggerFactory.Object, MockUserManager.Object, MockSessionManager.Object, MockLibraryManager.Object);
        var itemId = Guid.NewGuid();
        var playlist = new List<Guid> { itemId };
        group.PlayQueue.Reset();
        group.PlayQueue.SetPlaylist(playlist);

        Assert.Single(group.PlayQueue.GetPlaylist());
        Assert.Equal(itemId, group.PlayQueue.GetPlaylist()[0].ItemId);

        var user = new User("test-user", "auth-provider", "pwdreset-provider");
        var result = group.HasAccessToPlayQueue(user);

        Assert.False(result);
    }

    [Fact]
    public void CreateGroup_WithDefaultRequest_IsPublicAndVisibleToStrangers()
    {
        var group = new Emby.Server.Implementations.SyncPlay.Group(MockLoggerFactory.Object, MockUserManager.Object, MockSessionManager.Object, MockLibraryManager.Object);
        var creatorUserId = Guid.NewGuid();

        group.CreateGroup(CreateSession(creatorUserId), new NewGroupRequest("test-group"), CancellationToken.None);

        Assert.Equal(SyncPlayGroupVisibility.Public, group.GetInfo().Visibility);
        Assert.True(group.IsVisibleTo(Guid.NewGuid()));
        Assert.True(group.CanJoin(Guid.NewGuid()));
    }

    [Fact]
    public void CreateGroup_PrivateGroup_VisibleOnlyToMembers()
    {
        var group = new Emby.Server.Implementations.SyncPlay.Group(MockLoggerFactory.Object, MockUserManager.Object, MockSessionManager.Object, MockLibraryManager.Object);
        var creatorUserId = Guid.NewGuid();
        var strangerUserId = Guid.NewGuid();

        group.CreateGroup(CreateSession(creatorUserId), new NewGroupRequest("test-group", SyncPlayGroupVisibility.Private), CancellationToken.None);

        Assert.Equal(SyncPlayGroupVisibility.Private, group.GetInfo().Visibility);
        Assert.Equal(creatorUserId, group.CreatedByUserId);
        Assert.True(group.HasMember(creatorUserId));
        Assert.True(group.IsVisibleTo(creatorUserId));
        Assert.False(group.HasMember(strangerUserId));
        Assert.False(group.IsVisibleTo(strangerUserId));
        Assert.False(group.CanJoin(strangerUserId));
    }

    [Fact]
    public void AddOrRefreshInvite_PendingInvite_GrantsVisibilityUntilExpiry()
    {
        var group = new Emby.Server.Implementations.SyncPlay.Group(MockLoggerFactory.Object, MockUserManager.Object, MockSessionManager.Object, MockLibraryManager.Object);
        var creatorSession = CreateSession(Guid.NewGuid());
        var inviteeUserId = Guid.NewGuid();

        group.CreateGroup(creatorSession, new NewGroupRequest("test-group", SyncPlayGroupVisibility.Private), CancellationToken.None);

        Assert.False(group.IsVisibleTo(inviteeUserId));

        var info = group.AddOrRefreshInvite(creatorSession, inviteeUserId);

        Assert.NotNull(info);
        Assert.Equal(group.GroupId, info!.GroupId);
        Assert.Equal(creatorSession.UserId, info.InvitedByUserId);
        Assert.True(group.IsInvitePending(inviteeUserId));
        Assert.True(group.IsVisibleTo(inviteeUserId));
        Assert.True(group.CanJoin(inviteeUserId));
        Assert.False(group.HasLapsedInvite(inviteeUserId));

        // Expired invites no longer grant visibility but are distinguishable from "never invited".
        group.InviteLifetimeSeconds = -1;
        group.AddOrRefreshInvite(creatorSession, inviteeUserId);

        Assert.False(group.IsInvitePending(inviteeUserId));
        Assert.False(group.IsVisibleTo(inviteeUserId));
        Assert.True(group.HasLapsedInvite(inviteeUserId));
    }

    [Fact]
    public void MarkInviteDeclined_RevokesVisibility_ReinviteRestoresIt()
    {
        var group = new Emby.Server.Implementations.SyncPlay.Group(MockLoggerFactory.Object, MockUserManager.Object, MockSessionManager.Object, MockLibraryManager.Object);
        var creatorSession = CreateSession(Guid.NewGuid());
        var inviteeUserId = Guid.NewGuid();

        group.CreateGroup(creatorSession, new NewGroupRequest("test-group", SyncPlayGroupVisibility.Private), CancellationToken.None);
        group.AddOrRefreshInvite(creatorSession, inviteeUserId);

        Assert.True(group.MarkInviteDeclined(inviteeUserId));
        Assert.False(group.IsVisibleTo(inviteeUserId));
        Assert.True(group.HasLapsedInvite(inviteeUserId));
        Assert.False(group.MarkInviteDeclined(inviteeUserId));

        // An explicit re-invite clears the decline.
        group.AddOrRefreshInvite(creatorSession, inviteeUserId);
        Assert.True(group.IsInvitePending(inviteeUserId));
        Assert.True(group.CanJoin(inviteeUserId));
    }

    [Fact]
    public void SessionLeave_PastMemberOfPrivateGroup_CanStillSeeAndRejoin()
    {
        var group = new Emby.Server.Implementations.SyncPlay.Group(MockLoggerFactory.Object, MockUserManager.Object, MockSessionManager.Object, MockLibraryManager.Object);
        var creatorSession = CreateSession(Guid.NewGuid());

        group.CreateGroup(creatorSession, new NewGroupRequest("test-group", SyncPlayGroupVisibility.Private), CancellationToken.None);
        group.SessionLeave(creatorSession, new LeaveGroupRequest(), CancellationToken.None);

        Assert.False(group.HasMember(creatorSession.UserId));
        Assert.True(group.IsVisibleTo(creatorSession.UserId));
        Assert.True(group.CanJoin(creatorSession.UserId));
    }

    [Fact]
    public void AddOrRefreshInvite_ForExistingMember_ReturnsNull()
    {
        var group = new Emby.Server.Implementations.SyncPlay.Group(MockLoggerFactory.Object, MockUserManager.Object, MockSessionManager.Object, MockLibraryManager.Object);
        var creatorSession = CreateSession(Guid.NewGuid());

        group.CreateGroup(creatorSession, new NewGroupRequest("test-group", SyncPlayGroupVisibility.Private), CancellationToken.None);

        Assert.Null(group.AddOrRefreshInvite(creatorSession, creatorSession.UserId));
    }

    private SessionInfo CreateSession(Guid userId)
    {
        return new SessionInfo(MockSessionManager.Object, new Mock<ILogger>().Object)
        {
            Id = Guid.NewGuid().ToString("N"),
            UserId = userId,
            UserName = "test-user"
        };
    }
}
