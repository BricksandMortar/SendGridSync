using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading;
using com.bricksandmortarstudio.SendGridSync.Constants;
using com.bricksandmortarstudio.SendGridSync.DTO;
using com.bricksandmortarstudio.SendGridSync.Model;
using com.bricksandmortarstudio.SendGridSync.Util;
using Newtonsoft.Json;
using RestSharp;
using Rock;
using Rock.Data;
using Rock.Model;

namespace com.bricksandmortarstudio.SendGridSync.Helper
{
    public class SyncHelper
    {
        public static bool CheckCustomFields( string apiKey )
        {
            var customFields = ApiHelper.GetCustomFields( apiKey );
            if ( customFields == null )
            {
                throw new Exception( "Unable to examine SendGrid custom fields" );
            }
            if ( customFields.custom_fields.All( c => c.name != "title" ) )
            {
                bool result = ApiHelper.CreateCustomField( "title", "text", apiKey );
                if ( !result )
                {
                    return false;
                }
            }
            if ( customFields.custom_fields.All( c => c.name != "person_alias_id" ) )
            {
                bool result = ApiHelper.CreateCustomField( "person_alias_id", "number", apiKey );
                if ( !result )
                {
                    return false;
                }
            }
            return true;
        }

        public static IQueryable<PersonAlias> FindNotYetSyncedPersonAlises( RockContext rockContext, IQueryable<int> populationIds,
            IQueryable<int> syncedPersonAliasIds )
        {
            var notSyncedPersonAliadsIds = populationIds.Except( syncedPersonAliasIds ).ToList();
            var personAliases = new PersonAliasService( rockContext ).GetByIds( notSyncedPersonAliadsIds );
            return personAliases.Where( a => notSyncedPersonAliadsIds.Contains( a.Id ) &&
                         a.Person.Email != null && a.Person.Email != string.Empty
                         && a.Person.EmailPreference == EmailPreference.EmailAllowed );
        }

        public static IQueryable<PersonAlias> FindNotYetSyncedPersonAlises( RockContext rockContext, IQueryable<int> syncedPersonAliasIds )
        {
            var personAliasService = new PersonAliasService( rockContext );

            var personAlises = personAliasService
                .Queryable()
                .AsNoTracking()
                .Where(
                    a =>
                        !syncedPersonAliasIds.Contains( a.Id ) && a.Person.IsEmailActive
                        && a.Person.Email != null && a.Person.Email != ""
                        && a.Person.EmailPreference == EmailPreference.EmailAllowed );
            return personAlises;
        }

        public static IQueryable<PersonAlias> FindResyncCandidates( RockContext rockContext, DateTime historicSyncMarker )
        {
            historicSyncMarker = historicSyncMarker.AddSeconds( 60 );
            var syncedPersonAliases = new PersonAliasHistoryService( rockContext )
                .Queryable()
                .Where( a => a.LastUpdated <=
                             historicSyncMarker && a.PersonAlias.Person.EmailPreference == EmailPreference.EmailAllowed &&
                             a.PersonAlias.Person.Email != null && a.PersonAlias.Person.Email != "" )
                .Select( a => a.PersonAlias );
            return syncedPersonAliases;
        }

        public static int SyncContacts( IQueryable<PersonAlias> personAliases, string apiKey, bool resyncing = false )
        {
            var restClient = new RestClient( SendGridRequest.SENDGRID_BASE_URL );
            var ordered = personAliases.OrderBy( f => f.Id );
            int syncCount = 0;
            foreach ( var chunk in ordered.QueryChunksOfSize( SendGridRequest.SENDGRID_ADD_RECEIPIENT_MAX_COUNT ) )
            {
                var request =
                    ApiHelper.AddContactsRequestBuilder( chunk, apiKey );
                var response = restClient.Execute( request );
                if ( response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created )
                {
                    var personAliasesEnumerated = ExtractUpdatedAliasIds( chunk, response );
                    if ( !resyncing )
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
                    throw new Exception( "One or more errors occurred syncing individuals." + Environment.NewLine +
                                        response.Content );
                }
                Thread.Sleep( 500 );
            }
            return syncCount;
        }

        private static IEnumerable<PersonAlias> ExtractUpdatedAliasIds( IEnumerable<PersonAlias> personAliases,
            IRestResponse response )
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
            var indicesToRemoveSorted = allIndicestoRemove.OrderByDescending( i => i ).Distinct();
            foreach ( int index in indicesToRemoveSorted )
            {
                personAlisesEnumerated.RemoveAt( index );
            }
            return personAlisesEnumerated;
        }

        private static int UpdateSendGridAliasHistory( IEnumerable<PersonAlias> personAliases )
        {
            var personAliasesEnumerated = personAliases as IList<PersonAlias> ?? personAliases.ToList();
            if ( !personAliasesEnumerated.Any() )
            {
                return 0;
            }
            var now = RockDateTime.Now;
            var rockContext = new RockContext();
            var personAliasHistoryService = new PersonAliasHistoryService( rockContext );
            int updated = 0;
            foreach ( var personalias in personAliasesEnumerated )
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

        public static int? EnsureListExists( string apiKey, string listName )
        {
            return ApiHelper.DoesListExist( listName, apiKey ) ?? ApiHelper.CreateNewList( apiKey, listName );
        }



        public static void AddPeopleToList( IEnumerable<PersonAlias> people, int listId, string apiKey )
        {
            var emails = people.Select( a => a.Person.Email ).OrderBy( a => a.Length );
            int emailCount = emails.Count();
            for ( int takenCount = 0;
                 takenCount < emailCount;
                 takenCount = takenCount + SendGridRequest.SENDGRID_ADD_RECEIPIENT_MAX_COUNT )
            {

                ApiHelper.AddPeopleToList(
                    emails.Skip( takenCount ).Take( SendGridRequest.SENDGRID_ADD_RECEIPIENT_MAX_COUNT ),
                    listId, apiKey );
            }

        }

        public static void EnsureValidPeopleOnly( IEnumerable<int> validPersonAlisIds, int listId, string apiKey )
        {
            int listCount = ApiHelper.GetListRecipientCount( listId, apiKey );
            var listPersonAliasIds = ApiHelper.GetListPersonAliasIds( apiKey, listId, listCount );
            var invalidPersonAliasIds = listPersonAliasIds.Except( validPersonAlisIds );
            var invalidPersonAliasIdsListed = invalidPersonAliasIds as IList<int> ?? invalidPersonAliasIds.ToList();
            if ( !invalidPersonAliasIdsListed.Any() )
            {
                return;
            }

            var emails = new PersonAliasService( new RockContext() ).GetByIds( invalidPersonAliasIdsListed.ToList() ).Where( pa => pa.Person.Email != null ).Select( pa => pa.Person.Email );
            ApiHelper.DeleteRecipients( apiKey, emails );
        }


    }
}
