using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.bricksandmortarstudio.SendGridSync.Constants
{
    public class SendGridRequest
    {
        public const string SENDGRID_BASE_URL = "https://api.sendgrid.com/v3/";
        public const string RECIPIENTS_RESOURCE = "contactdb/recipients";
        public const string CUSTOM_FIELDS_RESOURCE = "contactdb/custom_fields";
        public const int SENDGRID_ADD_RECEIPIENT_MAX_COUNT = 500;
        public const string LISTS_RESOURCE = "/contactdb/lists";
        public const string LISTS_PERSON_RESOUCE = "recipients";
    }
}
