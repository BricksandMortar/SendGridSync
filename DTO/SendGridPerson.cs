using Newtonsoft.Json;

namespace com.bricksandmortarstudio.SendGridSync.DTO
{
    internal class SendGridPerson
    {
        public SendGridPerson( string email, int aliasId, string firstName, string lastName, string title )
        {
            Email = email;
            PersonAliasId = aliasId;
            FirstName = firstName;
            LastName = lastName;
            Title = title;
        }
        public bool ShouldSerializeTitle()
        {
            return !string.IsNullOrEmpty( Title );
        }

        [JsonProperty( PropertyName = "person_alias_id" )]
        public int PersonAliasId { get; set; }
        [JsonProperty( PropertyName = "email" )]
        public string Email { get; set; }
        [JsonProperty( PropertyName = "first_name" )]
        public string FirstName { get; set; }
        [JsonProperty( PropertyName = "last_name" )]
        public string LastName { get; set; }
        [JsonProperty( PropertyName = "title", NullValueHandling = NullValueHandling.Ignore )]
        public string Title { get; set; }
    }
}