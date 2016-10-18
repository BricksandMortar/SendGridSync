using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using com.bricksandmortarstudio.SendGridSync.Model;
using Newtonsoft.Json;
using Quartz;
using RestSharp;
using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Security.ExternalAuthentication;

namespace com.bricksandmortarstudio.SendGridSync.Jobs
{
    [TextField( "API Key", "The SendGrid API key.", false, "", "", 1 )]
    [DisallowConcurrentExecution]
    public class UpdateContacts : IJob
    {
        private const string SENDGRID_BASE_URL = "https://api.ideal-postcodes.co.uk/";
        private const string ADD_MULTIPLE_PARTICIPANTS_RESOURCE = "contactdb/recipients";
        private string _apiKey;

        
        public void Execute( IJobExecutionContext context )
        {
            var dataMap = context.JobDetail.JobDataMap;
            _apiKey = dataMap.GetString("APIKey");
            var rockContext = new RockContext();

            var syncedPersonAliasIds = new PersonAliasHistoryService( rockContext ).Queryable().AsNoTracking().Select( a => a.PersonAliasId );
            var toSyncPersonAliases = FindNotYetSyncedPersonAlises(rockContext, syncedPersonAliasIds).ToList();
            var toReSyncPersonAliases = new List<PersonAlias>();
            if (context.PreviousFireTimeUtc.HasValue)
            {
               toReSyncPersonAliases = FindOldSyncedPeople(rockContext, context.PreviousFireTimeUtc.Value).ToList();
            }

            Sync( toSyncPersonAliases );
            if ( toReSyncPersonAliases.Any())
            {
                Sync( toReSyncPersonAliases, true );
            }
        }

        private static IEnumerable<PersonAlias> FindNotYetSyncedPersonAlises( RockContext rockContext, IQueryable<int> syncedPersonAliasIds )
        {
            var personAliasService = new PersonAliasService( rockContext );

            var personAlises = personAliasService
                .Queryable()
                .AsNoTracking()
                .Where(
                    a =>
                        syncedPersonAliasIds.Any(
                            s => s != a.Id ) );
            return personAlises;
        }

        private static IEnumerable<PersonAlias> FindOldSyncedPeople( RockContext rockContext, DateTimeOffset previousJobFireTimeOffset )
        {
            var previousFireTime = previousJobFireTimeOffset.DateTime.AddSeconds( 60 );
            var syncedPersonAliases = new PersonAliasHistoryService( rockContext ).Queryable().Where(a => a.LastUpdated <= previousFireTime ).Select( a => a.PersonAlias );
            return syncedPersonAliases;
        }

        private void Sync(IList<PersonAlias> personAliases, bool resyncing = false)
        {
            var restClient = new RestClient(SENDGRID_BASE_URL);
            var request = BuildContactsRequest(personAliases);
            var response = restClient.Execute(request);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                personAliases = ExtractUpdatedAliasIds(personAliases, response);
            }
            if (!resyncing)
            {
                AddToSendGridAliasHistory(personAliases);
            }
            else
            {
                UpdateSendGridAliasHistory(personAliases);
            }
        }

        private static IList<PersonAlias> ExtractUpdatedAliasIds(IList<PersonAlias> personAliases, IRestResponse response)
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };
            var sendGridResults = JsonConvert.DeserializeObject<SendGridResults>(response.Content, settings);
            var indicesToRemove = new List<int>();
            indicesToRemove.AddRange(sendGridResults.error_indices);
            indicesToRemove.AddRange(sendGridResults.unmodified_indices);
            foreach (int index in indicesToRemove)
            {
                personAliases.RemoveAt(index);
            }
            return personAliases;
        }

        private static void UpdateSendGridAliasHistory(IEnumerable<PersonAlias> personAliases)
        {
            var now = RockDateTime.Now;
            var rockContext = new RockContext();
            var personAliasHistoryService = new PersonAliasHistoryService(rockContext);
            foreach (var personalias in personAliases)
            {
                var personAliasHistory = personAliasHistoryService.GetByPersonAliasId(personalias.Id);
                if (personAliasHistory != null)
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
            foreach (var aliasId in personAliases)
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
            var payload = people.Select( p => new SendGridPerson( p.Person.Email, p.Id, p.Person.FirstName, p.Person.LastName, p.Person.TitleValue.Value ) ).ToList();
            var request = new RestRequest( Method.POST )
            {
                RequestFormat = DataFormat.Json,
                Resource = ADD_MULTIPLE_PARTICIPANTS_RESOURCE
            };
            request.AddHeader("Authorization", _apiKey);
            request.AddBody( payload );
            return request;
        }
    }

    internal class SendGridPerson
    {
        public SendGridPerson( string email, int aliasId, string firstName, string lastName, string title )
        {
            this.Email = email;
            this.PersonAliasId = aliasId;
            this.FirstName = firstName;
            this.LastName = lastName;
            this.Title = title;
        }
        [JsonProperty( PropertyName = "person_alias_id" )]
        public int PersonAliasId { get; set; }
        [JsonProperty( PropertyName = "email" )]
        public string Email { get; set; }
        [JsonProperty( PropertyName = "first_name" )]
        public string FirstName { get; set; }
        [JsonProperty( PropertyName = "last_name" )]
        public string LastName { get; set; }
        [JsonProperty( PropertyName = "title" )]
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
    // ReSharper restore InconsistentNaming

}
