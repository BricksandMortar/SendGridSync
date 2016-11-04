using System.Diagnostics.CodeAnalysis;

namespace com.bricksandmortarstudio.SendGridSync.DTO
{
    [SuppressMessage( "ReSharper", "InconsistentNaming" )]
    public class List
    {
        public int id { get; set; }
        public string name { get; set; }
        public int recipient_count { get; set; }
    }
}
