using System;
using System.Linq;
using System.Threading;
using Emby.Server.Implementations.SyncPlay;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Controller.SyncPlay.Requests;
using MediaBrowser.Model.SyncPlay;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.SyncPlay;

public class SyncPlayManagerTests
{
    public SyncPlayManagerTests()
    {
        var mockLogger = new Mock<ILogger<SyncPlayManager>>();
        MockLoggerFactory = new Mock<ILoggerFactory>();
        MockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

        MockUserManager = new Mock<IUserManager>();
        MockSessionManager = new Mock<ISessionManager>();
        MockLibraryManager = new Mock<ILibraryManager>();

        Manager = new SyncPlayManager(MockLoggerFactory.Object, MockUserManager.Object, MockSessionManager.Object, MockLibraryManager.Object);
    }

    private Mock<ILoggerFactory> MockLoggerFactory { get; }

    private Mock<IUserManager> MockUserManager { get; }

    private Mock<ISessionManager> MockSessionManager { get; }

    private Mock<ILibraryManager> MockLibraryManager { get; }

    private SyncPlayManager Manager { get; }

    [Fact]
    public void ListGroupsAndGetGroup_PrivateGroup_HiddenFromStrangers()
    {
        var creatorSession = CreateSession("creator");
        var strangerSession = CreateSession("stranger");

        var groupInfo = Manager.NewGroup(creatorSession, new NewGroupRequest("private-group", SyncPlayGroupVisibility.Private), CancellationToken.None);

        Assert.Empty(Manager.ListGroups(strangerSession, new ListGroupsRequest()));
        Assert.Null(Manager.GetGroup(strangerSession, groupInfo.GroupId));

        var visible = Assert.Single(Manager.ListGroups(creatorSession, new ListGroupsRequest()));
        Assert.Equal(SyncPlayGroupVisibility.Private, visible.Visibility);
        Assert.NotNull(Manager.GetGroup(creatorSession, groupInfo.GroupId));
    }

    [Fact]
    public void JoinGroup_PrivateGroupWithoutInvite_RejectedWithGroupDoesNotExist()
    {
        var creatorSession = CreateSession("creator");
        var strangerSession = CreateSession("stranger");

        var groupInfo = Manager.NewGroup(creatorSession, new NewGroupRequest("private-group", SyncPlayGroupVisibility.Private), CancellationToken.None);

        Manager.JoinGroup(strangerSession, new JoinGroupRequest(groupInfo.GroupId), CancellationToken.None);

        // The stranger was not added and the error denies the group's existence with an empty group id.
        var info = Manager.GetGroup(creatorSession, groupInfo.GroupId);
        Assert.NotNull(info);
        Assert.Single(info!.Participants);
        MockSessionManager.Verify(
            m => m.SendSyncPlayGroupUpdate(strangerSession.Id, It.Is<SyncPlayGroupDoesNotExistUpdate>(u => u.GroupId.Equals(Guid.Empty)), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void JoinGroup_PrivateGroupWithPendingInvite_Succeeds()
    {
        var creatorSession = CreateSession("creator");
        var inviteeSession = CreateSession("invitee");

        var groupInfo = Manager.NewGroup(creatorSession, new NewGroupRequest("private-group", SyncPlayGroupVisibility.Private), CancellationToken.None);
        Manager.InviteToGroup(creatorSession, new[] { inviteeSession.UserId }, CancellationToken.None);

        // The invite makes the group visible to the invitee.
        Assert.Single(Manager.ListGroups(inviteeSession, new ListGroupsRequest()));

        Manager.JoinGroup(inviteeSession, new JoinGroupRequest(groupInfo.GroupId), CancellationToken.None);

        var info = Manager.GetGroup(creatorSession, groupInfo.GroupId);
        Assert.NotNull(info);
        Assert.Equal(2, info!.Participants.Count);
    }

    [Fact]
    public void JoinGroup_PrivateGroupWithDeclinedInvite_RejectedWithInviteRequired()
    {
        var creatorSession = CreateSession("creator");
        var inviteeSession = CreateSession("invitee");

        var groupInfo = Manager.NewGroup(creatorSession, new NewGroupRequest("private-group", SyncPlayGroupVisibility.Private), CancellationToken.None);
        Manager.InviteToGroup(creatorSession, new[] { inviteeSession.UserId }, CancellationToken.None);
        Manager.DeclineInvite(inviteeSession, groupInfo.GroupId, CancellationToken.None);

        Manager.JoinGroup(inviteeSession, new JoinGroupRequest(groupInfo.GroupId), CancellationToken.None);

        var info = Manager.GetGroup(creatorSession, groupInfo.GroupId);
        Assert.NotNull(info);
        Assert.Single(info!.Participants);
        MockSessionManager.Verify(
            m => m.SendSyncPlayGroupUpdate(inviteeSession.Id, It.IsAny<SyncPlayGroupInviteRequiredUpdate>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void GetInviteCandidates_SessionNotInAnyGroup_ReturnsEmpty()
    {
        var strangerSession = CreateSession("stranger");

        Assert.Empty(Manager.GetInviteCandidates(strangerSession));
    }

    [Fact]
    public void ListInvites_PendingInvite_ReturnedForInvitee()
    {
        var creatorSession = CreateSession("creator");
        var inviteeSession = CreateSession("invitee");

        var groupInfo = Manager.NewGroup(creatorSession, new NewGroupRequest("private-group", SyncPlayGroupVisibility.Private), CancellationToken.None);
        Manager.InviteToGroup(creatorSession, new[] { inviteeSession.UserId }, CancellationToken.None);

        var invite = Assert.Single(Manager.ListInvites(inviteeSession));
        Assert.Equal(groupInfo.GroupId, invite.GroupId);
        Assert.Equal(creatorSession.UserId, invite.InvitedByUserId);
        Assert.Empty(Manager.ListInvites(creatorSession));
    }

    private SessionInfo CreateSession(string userName)
    {
        var userId = Guid.NewGuid();
        var user = new User(userName, "auth-provider", "pwdreset-provider")
        {
            SyncPlayAccess = SyncPlayUserAccessType.CreateAndJoinGroups
        };
        MockUserManager.Setup(m => m.GetUserById(userId)).Returns(user);

        return new SessionInfo(MockSessionManager.Object, new Mock<ILogger>().Object)
        {
            Id = Guid.NewGuid().ToString("N"),
            UserId = userId,
            UserName = userName
        };
    }
}
