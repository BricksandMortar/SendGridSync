using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Text;
using com.bricksandmortarstudio.SendGridSync.Constants;
using com.bricksandmortarstudio.SendGridSync.DTO;
using com.bricksandmortarstudio.SendGridSync.Model;
using Newtonsoft.Json;
using RestSharp;
using Rock;
using Rock.Data;
using Rock.Model;

namespace com.bricksandmortarstudio.SendGridSync.Utils
{
    public class SendGridRequestUtil
    {
        public static bool CheckCustomFields(string apiKey)
        {
            var customFields = GetCustomFields( apiKey );
            if ( customFields == null )
            {
                throw new Exception( "Unable to examine SendGrid custom fields" );
            }
            if ( Enumerable.All<CustomField>(customFields.custom_fields, c => c.name != "title" ) )
            {
                bool result = CreateCustomField( "title", "text", apiKey );
                if ( !result )
                {
                    return false;
                }
            }
            if ( Enumerable.All<CustomField>(customFields.custom_fields, c => c.name != "person_alias_id" ) )
            {
                bool result = CreateCustomField( "person_alias_id", "number", apiKey );
                if ( !result )
                {
                    return false;
                }
            }
            return true;
        }

        private static bool CreateCustomField( string name, string type, string apiKey )
        {
            var restClient = new RestClient(SendGridRequest.SENDGRID_BASE_URL );
            var request = new RestRequest( Method.POST )
            {
                RequestFormat = DataFormat.Json,
                Resource = SendGridRequest.CUSTOM_FIELDS_RESOURCE
            };
            request.AddHeader( "Authorization", "Bearer " + apiKey );

            request.AddBody( new { name = name, type = type } );
            var response = restClient.Execute( request );
            if ( response.StatusCode == HttpStatusCode.Created )
            {
                return true;
            }
            throw new Exception( string.Format("Unable to create SendGrid custom field {0}", name) );
        }

        public static CustomFields GetCustomFields(string apiKey )
        {
            var restClient = new RestClient(SendGridRequest.SENDGRID_BASE_URL );
            var request = new RestRequest( Method.GET )
            {
                RequestFormat = DataFormat.Json,
                Resource = SendGridRequest.CUSTOM_FIELDS_RESOURCE
            };
            request.AddHeader( "Authorization", "Bearer " + apiKey );
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
            throw new Exception( "Unable to obtain existing custom fields from SendGrid");
        }

        public static IQueryable<PersonAlias> FindNotYetSyncedPersonAlises( RockContext rockContext, IQueryable<int> syncedPersonAliasIds )
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

        public static IQueryable<PersonAlias> FindOldSyncedPeople( RockContext rockContext, DateTime historicSyncMarker )
        {
            historicSyncMarker = historicSyncMarker.AddSeconds( 60 );
            var syncedPersonAliases = new PersonAliasHistoryService( rockContext )
                .Queryable()
                .Where( a => a.LastUpdated <=
                             historicSyncMarker && a.PersonAlias.Person.EmailPreference == EmailPreference.EmailAllowed && a.PersonAlias.Person.Email != null && a.PersonAlias.Person.Email != "" )
                .Select( a => a.PersonAlias );
            return syncedPersonAliases;
        }

        public static int SyncContacts( IQueryable<PersonAlias> personAliases, string apiKey, bool resyncing = false )
        {
            var restClient = new RestClient(SendGridRequest.SENDGRID_BASE_URL );

            int personAliasesCount = personAliases.Count();
            int syncCount = 0;
            for ( int takenCount = 0; takenCount < personAliasesCount; takenCount = takenCount + Constants.SendGridRequest.SENDGRID_ADD_RECEIPIENT_MAX_COUNT )
            {
                var request = BuildContactsRequest( personAliases.Skip( takenCount ).Take( Constants.SendGridRequest.SENDGRID_ADD_RECEIPIENT_MAX_COUNT ), apiKey );
                var response = restClient.Execute( request );
                if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
                {
                    var personAliasesEnumerated = ExtractUpdatedAliasIds(personAliases, response);
                    if (!resyncing)
                    {
                        syncCount += AddToSendGridAliasHistory( personAliasesEnumerated );
                    }
                    else
                    {
                        syncCount += UpdateSendGridAliasHistory( personAliasesEnumerated );
                    }
                }
                else
                {
                    throw new Exception( "One or more errors occurred syncing individuals." + Environment.NewLine + response.Content );
                }
            }
            return syncCount;
        }

        private static IList<PersonAlias> ExtractUpdatedAliasIds( IQueryable<PersonAlias> personAliases, IRestResponse response )
        {
            var personAlisesEnumerated = personAliases.ToList();
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
                personAlisesEnumerated.RemoveAt( index );
            }
            return personAlisesEnumerated;
        }

        private static int UpdateSendGridAliasHistory( IEnumerable<PersonAlias> personAliases )
        {
            if (!personAliases.Any())
            {
                return 0;
            }
            var now = RockDateTime.Now;
            var rockContext = new RockContext();
            var personAliasHistoryService = new PersonAliasHistoryService( rockContext );
            int updated = 0;
            foreach ( var personalias in personAliases )
            {
                var personAliasHistory = personAliasHistoryService.GetByPersonAliasId( personalias.Id );
                if ( personAliasHistory != null )
                {
                    personAliasHistory.LastUpdated = now;
                }
                updated++;
            }
            rockContext.SaveChanges();
            return updated;
        }

        private static int AddToSendGridAliasHistory( IEnumerable<PersonAlias> personAliases )
        {
            var now = RockDateTime.Now;
            var rockContext = new RockContext();
            var personAliasHistoryService = new PersonAliasHistoryService( rockContext );
            int synced = 0;
            foreach ( var aliasId in personAliases )
            {
                var personAliasHistory = new PersonAliasHistory
                {
                    PersonAliasId = aliasId.Id,
                    LastUpdated = now
                };
                personAliasHistoryService.Add( personAliasHistory );
                synced++;
            }
            rockContext.SaveChanges();
            return synced;
        }

        private static IRestRequest BuildContactsRequest( IEnumerable<PersonAlias> people, string apiKey )
        {
            var payload = people.Select( p => new SendGridPerson( p.Person.Email, p.Id, p.Person.FirstName, p.Person.LastName, p.Person.TitleValue != null ? p.Person.TitleValue.Value : "" ) ).ToList();
            var request = new RestRequest( Method.POST )
            {
                RequestFormat = DataFormat.Json,
                Resource = SendGridRequest.RECIPIENTS_RESOURCE
            };
            request.AddHeader( "Authorization", "Bearer " + apiKey );
            request.AddParameter("application/json", JsonConvert.SerializeObject( payload,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                } ), ParameterType.RequestBody );
            return request;
        }

        private static IRestRequest CheckIfListExists(string listName, string apiKey)
        {
            var restClient = new RestClient( SendGridRequest.SENDGRID_BASE_URL );
            var request = new RestRequest( Method.GET )
            {
                RequestFormat = DataFormat.Json,
                Resource = SendGridRequest.CUSTOM_FIELDS_RESOURCE
            };
            request.AddHeader( "Authorization", "Bearer " + apiKey );
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
            throw new Exception( "Unable to obtain existing custom fields from SendGrid" );
        }

        private static string GetRecipientId(string email)
        {
            return Convert.ToBase64String( Encoding.ASCII.GetBytes( email ) );
        }
    }
}