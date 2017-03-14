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
using Rock.Web.Cache;

namespace com.bricksandmortarstudio.SendGridSync.Jobs
{
    [TextField( "API Key", "The SendGrid API key.", false, "", "", order:3 )]
    [GroupField("Synced Group", "The group that should be synced with a given SendGrid List", true, key:"group", order:1)]
    [IntegerField("Existing Person Update Interval", "The number of days before an existing SendGrid record should be resynced", true, 7, key: "dayInterval", order:2 )]
    [TextField("List Name", "The SendGrid list to sync to")]
    [IntegerField( "Timeout", "The number of seconds to use before the database connection times out", true, 720 )]
    [DisallowConcurrentExecution]
    public class SyncGrouptoList : IJob
    {
        public void Execute( IJobExecutionContext context )
        {
            var dataMap = context.JobDetail.JobDataMap;
            string apiKey = dataMap.GetString( "APIKey" );
            string listName = dataMap.GetString("ListName");
            string groupGuid = dataMap.GetString("group");
            int timeout = dataMap.GetIntFromString( "Timeout" );

            var group = groupGuid.AsGuidOrNull();

            //Check API Key exists and group guid is not null
            if ( string.IsNullOrWhiteSpace( apiKey ) || !group.HasValue || string.IsNullOrWhiteSpace( listName ) )
            {
                return;
            }

            //Check all the custom fields have been created for the SendGrid marketing campaign.
            bool fieldsExist = SyncHelper.CheckCustomFields( apiKey );
            if ( !fieldsExist )
            {
                return;
            }

            int? listId = SyncHelper.EnsureListExists( apiKey, listName );
            if ( !listId.HasValue )
            {
                throw new Exception( "Unable to identify list identifier" );
            }

            var rockContext = new RockContext();
            rockContext.Database.CommandTimeout = 720;

            int activeRecordStatusValueId = DefinedValueCache.Read(Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_ACTIVE).Id;

            var groupMemberAliases = new GroupMemberService(rockContext)
                .Queryable()
                .AsNoTracking()
                .Where(gm => gm.Group.Guid == group.Value)
                .Select( gm => gm.Person ) 
                .Where( p => !p.IsDeceased && p.RecordStatusValueId == activeRecordStatusValueId && p.IsEmailActive && p.Email != null && p.Email != string.Empty && p.EmailPreference == EmailPreference.EmailAllowed)
                .Select(p => p.Aliases.FirstOrDefault());

            var groupMemberAliasIds = groupMemberAliases.Select(a => a.Id);

            var previouslySyncedPersonAliasIds = new PersonAliasHistoryService( rockContext ).GetPreviouslySyncedPersonAliasIds( groupMemberAliasIds );

            var notYetSynced = SyncHelper.FindNotYetSyncedPersonAlises(rockContext, groupMemberAliasIds, previouslySyncedPersonAliasIds);

            if (notYetSynced.Any())
            {
                SyncHelper.SyncContacts( notYetSynced, apiKey );
            }

            if (groupMemberAliasIds.Any())
            {
                SyncHelper.EnsureValidPeopleOnly( groupMemberAliasIds, listId.Value, apiKey );
                SyncHelper.AddPeopleToList( groupMemberAliases, listId.Value, apiKey );
            }


            context.Result = "Group synced successfully";
        }
    }

}
