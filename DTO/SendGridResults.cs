using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace com.bricksandmortarstudio.SendGridSync.DTO
{
    [SuppressMessage( "ReSharper", "InconsistentNaming" )]
    internal class SendGridResults
    {

        public int error_count { get; set; }
        public List<int> error_indices { get; set; }
        public List<int> unmodified_indices { get; set; }
        public int new_count { get; set; }
        public List<string> persisted_recipients { get; set; }
        public int updated_count { get; set; }
        public List<Error> errors { get; set; }
    }
}