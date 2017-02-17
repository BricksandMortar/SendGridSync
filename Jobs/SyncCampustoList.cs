using System;
using System.Data.Entity;
using System.Linq;
using com.bricksandmortarstudio.SendGridSync.Model;
using com.bricksandmortarstudio.SendGridSync.Helper;
using Quartz;
using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;

namespace com.bricksandmortarstudio.SendGridSync.Jobs
{
    [TextField( "API Key", "The SendGrid API key.", false, "", "", order: 3 )]
    [CampusField( "Campus", "The campus that should be synced", true, "", false, key: "campus", order: 1 )]
    [IntegerField( "Existing Person Update Interval", "The number of days before an existing SendGrid record should be resynced", true, 7, key: "dayInterval", order: 2 )]
    [TextField( "List Name", "The SendGrid" )]
    [DisallowConcurrentExecution]
    public class SyncCampustoList : IJob
    {
        public void Execute( IJobExecutionContext context )
        {
            var dataMap = context.JobDetail.JobDataMap;
            string apiKey = dataMap.GetString( "APIKey" );
            string listName = dataMap.GetString( "ListName" );
            string campusGuid = dataMap.GetString( "campus" );

            var campus = campusGuid.AsGuidOrNull();

            //Check API Key exists and group guid is not null
            if ( string.IsNullOrWhiteSpace( apiKey ) || !campus.HasValue || string.IsNullOrWhiteSpace( listName ) )
            {
                return;
            }

            //Check all the custom fields have been created for the SendGrid marketing campaign.
            bool fieldsExist = SyncHelper.CheckCustomFields( apiKey );
            if ( !fieldsExist )
            {
                return;
            }

            var listId = SyncHelper.EnsureListExists( apiKey, listName );
            if ( !listId.HasValue )
            {
                throw new Exception( "Unable to identify list identifier" );
            }

            var familyGuid = Rock.SystemGuid.GroupType.GROUPTYPE_FAMILY.AsGuid();

            var rockContext = new RockContext();
            rockContext.Database.CommandTimeout = 360;
            var campusAttendeesPersonAlias = new GroupService( rockContext )
                .Queryable()
                .AsNoTracking()
                .Where( g => g.GroupType.Guid == familyGuid && g.Campus.Guid == campus.Value )
                .SelectMany( g => g.Members )
                .Select( gm => gm.Person )
                .Where( p => p.IsEmailActive && p.Email != null && p.Email != string.Empty && p.EmailPreference == EmailPreference.EmailAllowed )
                .Select( p => p.Aliases.FirstOrDefault() );
            var campusAttendeesPersonAliasIds = campusAttendeesPersonAlias.Select( a => a.Id );

            var previouslySyncedPersonAliasIds = new PersonAliasHistoryService( rockContext ).GetPreviouslySyncedPersonAliasIds(campusAttendeesPersonAliasIds );
            var notYetSynced = SyncHelper.FindNotYetSyncedPersonAlises( rockContext, campusAttendeesPersonAliasIds, previouslySyncedPersonAliasIds );
            if (notYetSynced.Any())
            {
                SyncHelper.SyncContacts( notYetSynced, apiKey );
            }

            if (campusAttendeesPersonAlias.Any())
            {
                SyncHelper.EnsureValidPeopleOnly( campusAttendeesPersonAliasIds, listId.Value, apiKey );
                SyncHelper.AddPeopleToList( campusAttendeesPersonAlias, listId.Value, apiKey );
            }


            context.Result = "Campus synced successfully";
        }
    }

}
