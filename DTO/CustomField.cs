using System.Diagnostics.CodeAnalysis;

namespace com.bricksandmortarstudio.SendGridSync.DTO
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class CustomField
    {
        public int id { get; set; }
        public string name { get; set; }
        public string type { get; set; }
    }
}