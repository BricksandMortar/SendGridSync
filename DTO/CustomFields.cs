using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace com.bricksandmortarstudio.SendGridSync.DTO
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class CustomFields
    {
        public List<CustomField> custom_fields { get; set; }
    }
}