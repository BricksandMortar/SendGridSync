using System.Collections.Generic;

namespace com.bricksandmortarstudio.SendGridSync.DTO
{
    internal class Error
    {
        public string message { get; set; }
        public List<int> error_indices { get; set; }
    }
}