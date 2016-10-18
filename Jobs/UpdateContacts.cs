using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using com.bricksandmortarstudio.SendGridSync.Model;
using Newtonsoft.Json;
using Quartz;
using RestSharp;
using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;

namespace com.bricksandmortarstudio.SendGridSync.Jobs
{
    [TextField( "API Key", "The SendGrid API key.", false, "", "", 1 )]
    [IntegerField("Existing Person Update Interval", "The number of days before an existing SendGrid record should be resynced", true, 7, key: "dayInterval" )]
    [DisallowConcurrentExecution]
    public class UpdateContacts : IJob
    {
        private const string SENDGRID_BASE_URL = "https://api.sendgrid.com/v3/";
        private const string RECIPIENTS_RESOURCE = "contactdb/recipients";
        private const string CUSTOM_FIELDS_RESOURCE = "contactdb/custom_fields";
        private const int SENDGRID_ADD_RECEIPIENT_MAX_COUNT = 200;
        private string _apiKey;

        public void Execute( IJobExecutionContext context )
        {
            var dataMap = context.JobDetail.JobDataMap;
            _apiKey = dataMap.GetString( "APIKey" );
            int dayInterval = dataMap.GetIntValue("dayInterval");

            //Check API Key exists
            if ( string.IsNullOrWhiteSpace( _apiKey ) )
            {
                return;
            }

            //Check all the custom fields have been created for the SendGrid marketing campaign.
            bool fieldsExist = CheckCustomFields();
            if ( !fieldsExist )
            {
                return;
            }

            var rockContext = new RockContext();
            var previouslySyncedPersonAliasIds = new PersonAliasHistoryService( rockContext ).Queryable().AsNoTracking().Select( a => a.PersonAliasId );
            var notYetSynced = FindNotYetSyncedPersonAlises( rockContext, previouslySyncedPersonAliasIds ).ToList();

            var historicSyncMarker = RockDateTime.Now.AddDays(dayInterval);
            var needReSyncPersonAliases = FindOldSyncedPeople( rockContext, historicSyncMarker ).ToList();

            Sync( notYetSynced );
            if ( needReSyncPersonAliases.Any() )
            {
                Sync( needReSyncPersonAliases, true );
            }
        }

        private bool CheckCustomFields()
        {
            var customFields = GetCustomFields();
            if ( customFields == null )
            {
                return false;
            }
            if ( customFields.custom_fields.All( c => c.name != "title" ) )
            {
                bool result = CreateCustomField( "title", "text" );
                if ( !result )
                {
                    return false;
                }
            }
            if ( customFields.custom_fields.All( c => c.name != "person_alias_id" ) )
            {
                bool result = CreateCustomField( "person_alias_id", "number" );
                if ( !result )
                {
                    return false;
                }
            }
            return true;
        }

        private bool CreateCustomField( string name, string type )
        {
            var restClient = new RestClient( SENDGRID_BASE_URL );
            var request = new RestRequest( Method.POST )
            {
                RequestFormat = DataFormat.Json,
                Resource = CUSTOM_FIELDS_RESOURCE
            };
            request.AddHeader( "Authorization", "Bearer " + _apiKey );

            request.AddBody( new { name = name, type = type } );
            var response = restClient.Execute( request );
            if ( response.StatusCode == HttpStatusCode.Created )
            {
                return true;
            }
            return false;
        }

        private CustomFields GetCustomFields()
        {
            var restClient = new RestClient( SENDGRID_BASE_URL );
            var request = new RestRequest( Method.GET )
            {
                RequestFormat = DataFormat.Json,
                Resource = CUSTOM_FIELDS_RESOURCE
            };
            request.AddHeader( "Authorization", "Bearer " + _apiKey );
            var response = restClient.Execute( request );
            if ( response.StatusCode == HttpStatusCode.OK )
            {
                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore
                };
                return JsonConvert.DeserializeObject<CustomFields>( response.Content, settings );
            }
            return null;
        }

        private static IEnumerable<PersonAlias> FindNotYetSyncedPersonAlises( RockContext rockContext, IQueryable<int> syncedPersonAliasIds )
        {
            var personAliasService = new PersonAliasService( rockContext );

            var personAlises = personAliasService
                .Queryable("Person")
                .AsNoTracking()
                .Where(
                    a =>
                        !syncedPersonAliasIds.Any(
                            s => s == a.Id ) && 
                            a.Person.Email != null && a.Person.Email != ""
                            && a.Person.EmailPreference == EmailPreference.EmailAllowed);
            return personAlises;
        }

        private static IEnumerable<PersonAlias> FindOldSyncedPeople( RockContext rockContext, DateTime historicSyncMarker )
        {
            var previousFireTime = historicSyncMarker.AddSeconds( 60 );
            var syncedPersonAliases = new PersonAliasHistoryService( rockContext )
                .Queryable()
                .Where( a => a.LastUpdated <= previousFireTime && a.PersonAlias.Person.EmailPreference == EmailPreference.EmailAllowed && a.PersonAlias.Person.Email != null && a.PersonAlias.Person.Email != "" )
                .Select( a => a.PersonAlias );
            return syncedPersonAliases;
        }

        private void Sync( IList<PersonAlias> personAliases, bool resyncing = false )
        {
            var restClient = new RestClient( SENDGRID_BASE_URL );

            for ( int takenCount = 0; takenCount < personAliases.Count; takenCount = takenCount + SENDGRID_ADD_RECEIPIENT_MAX_COUNT )
            {
                var request = BuildContactsRequest( personAliases.Skip( takenCount ).Take( SENDGRID_ADD_RECEIPIENT_MAX_COUNT ) );
                var response = restClient.Execute( request );
                if ( response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created )
                {
                    personAliases = ExtractUpdatedAliasIds( personAliases, response );
                    if ( !resyncing )
                    {
                        AddToSendGridAliasHistory( personAliases );
                    }
                    else
                    {
                        UpdateSendGridAliasHistory( personAliases );
                    }
                }
            }
        }

        private static IList<PersonAlias> ExtractUpdatedAliasIds( IList<PersonAlias> personAliases, IRestResponse response )
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };
            var sendGridResults = JsonConvert.DeserializeObject<SendGridResults>( response.Content, settings );
            var allIndicestoRemove = new List<int>();
            allIndicestoRemove.AddRange( sendGridResults.error_indices );
            allIndicestoRemove.AddRange( sendGridResults.unmodified_indices );
            var indicesToRemoveSorted = allIndicestoRemove.OrderByDescending(i => i).Distinct();
            foreach ( int index in indicesToRemoveSorted )
            {
                personAliases.RemoveAt( index );
            }
            return personAliases;
        }

        private static void UpdateSendGridAliasHistory( IEnumerable<PersonAlias> personAliases )
        {
            var now = RockDateTime.Now;
            var rockContext = new RockContext();
            var personAliasHistoryService = new PersonAliasHistoryService( rockContext );
            foreach ( var personalias in personAliases )
            {
                var personAliasHistory = personAliasHistoryService.GetByPersonAliasId( personalias.Id );
                if ( personAliasHistory != null )
                {
                    personAliasHistory.LastUpdated = now;
                }
            }
            rockContext.SaveChanges();
        }

        private static void AddToSendGridAliasHistory( IEnumerable<PersonAlias> personAliases )
        {
            var now = RockDateTime.Now;
            var rockContext = new RockContext();
            var personAliasHistoryService = new PersonAliasHistoryService( rockContext );
            foreach ( var aliasId in personAliases )
            {
                var personAliasHistory = new PersonAliasHistory
                {
                    PersonAliasId = aliasId.Id,
                    LastUpdated = now
                };
                personAliasHistoryService.Add( personAliasHistory );
            }
            rockContext.SaveChanges();
        }

        private IRestRequest BuildContactsRequest( IEnumerable<PersonAlias> people )
        {
            var payload = people.Select( p => new SendGridPerson( p.Person.Email, p.Id, p.Person.FirstName, p.Person.LastName, p.Person.TitleValue != null ? p.Person.TitleValue.Value : "" ) ).ToList();
            var request = new RestRequest( Method.POST )
            {
                RequestFormat = DataFormat.Json,
                Resource = RECIPIENTS_RESOURCE
            };
            request.AddHeader( "Authorization", "Bearer " + _apiKey );
            request.AddParameter("application/json", JsonConvert.SerializeObject( payload,
                            new JsonSerializerSettings
                            {
                                NullValueHandling = NullValueHandling.Ignore,
                            } ), ParameterType.RequestBody );
            return request;
        }
    }

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

    // ReSharper disable InconsistentNaming
    internal class Error
    {
        public string message { get; set; }
        public List<int> error_indices { get; set; }
    }

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

    public class CustomField
    {
        public int id { get; set; }
        public string name { get; set; }
        public string type { get; set; }
    }

    public class CustomFields
    {
        public List<CustomField> custom_fields { get; set; }
    }
    // ReSharper restore InconsistentNaming

}
