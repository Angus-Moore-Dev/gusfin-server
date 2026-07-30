using MediaBrowser.Model.SyncPlay;

namespace MediaBrowser.Controller.SyncPlay.Requests
{
    /// <summary>
    /// Class NewGroupRequest.
    /// </summary>
    public class NewGroupRequest : ISyncPlayRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NewGroupRequest"/> class.
        /// </summary>
        /// <param name="groupName">The name of the new group.</param>
        /// <param name="visibility">The visibility of the new group. Gusfin extension.</param>
        public NewGroupRequest(string groupName, SyncPlayGroupVisibility visibility = SyncPlayGroupVisibility.Public)
        {
            GroupName = groupName;
            Visibility = visibility;
        }

        /// <summary>
        /// Gets the group name.
        /// </summary>
        /// <value>The name of the new group.</value>
        public string GroupName { get; }

        /// <summary>
        /// Gets the group visibility. Gusfin extension.
        /// </summary>
        /// <value>The visibility of the new group.</value>
        public SyncPlayGroupVisibility Visibility { get; }

        /// <inheritdoc />
        public RequestType Type { get; } = RequestType.NewGroup;
    }
}
