namespace Unity.Services.Multiplayer.Components
{
    /// <summary>
    /// Base URL for this package's published API documentation.
    /// </summary>
    internal static class DocsUrl
    {
        /// <summary>
        /// The <c>@x.y</c> segment is asserted against <c>package.json</c>'s version by
        /// <c>HelpUrlTests</c>, so a build that bumps the package version without updating
        /// this constant fails loudly instead of shipping a stale help link.
        /// </summary>
        public const string PackageBase = "https://docs.unity3d.com/Packages/com.unity.services.multiplayer@2.3/";
    }
}
