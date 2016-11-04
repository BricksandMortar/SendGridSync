using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace com.bricksandmortarstudio.SendGridSync.DTO
{
    [SuppressMessage( "ReSharper", "InconsistentNaming" )]
    internal class Error
    {
        public string message { get; set; }
        public List<int> error_indices { get; set; }
    }
}