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
    [TextField( "API Key", "The SendGrid API key.", false, "", "", order:3 )]
    [GroupField("Synced Group", "The group that should be synced with a given SendGrid List", true, key:"group", order:1)]
    [IntegerField("Existing Person Update Interval", "The number of days before an existing SendGrid record should be resynced", true, 7, key: "dayInterval", order:2 )]
    [TextField("List Name", "The SendGrid")]
    [DisallowConcurrentExecution]
    public class SyncGrouptoList : IJob
    {
        public void Execute( IJobExecutionContext context )
        {
            var dataMap = context.JobDetail.JobDataMap;
            string apiKey = dataMap.GetString( "APIKey" );
            string listName = dataMap.GetString("ListName");
            string groupGuid = dataMap.GetString("group");

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
            rockContext.Database.CommandTimeout = 360;

            var groupMemberAliases = new GroupMemberService(rockContext)
                .Queryable()
                .AsNoTracking()
                .Where(a => a.Group.Guid == group.Value && a.Person.IsEmailActive && a.Person.Email != null && a.Person.Email != string.Empty && a.Person.EmailPreference == EmailPreference.EmailAllowed)
                .Select(a => a.Person.Aliases.FirstOrDefault());

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
