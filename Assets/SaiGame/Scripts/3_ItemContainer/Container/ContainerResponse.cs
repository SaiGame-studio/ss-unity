using System;

namespace SaiGame.Services
{
    /// <summary>
    /// Represents the paginated response returned by the containers endpoint.
    /// </summary>
    [Serializable]
    public class ContainerResponse
    {
        public ContainerData[] containers;
        public bool has_more;
        public int limit;
        public int offset;
    }
}
