using System.Collections.Generic;

namespace EFModeling.CustomFunctionMapping
{
    #region Entity
    public class Tag
    {
        public string TagId { get; set; }

        public List<Post> Posts { get; set; }
    }
    #endregion
}
