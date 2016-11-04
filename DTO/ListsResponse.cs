using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace com.bricksandmortarstudio.SendGridSync.DTO
{
    [SuppressMessage( "ReSharper", "InconsistentNaming" )]
    public class ListsResponse
    {
        public List<List> lists { get; set; }
    }
}