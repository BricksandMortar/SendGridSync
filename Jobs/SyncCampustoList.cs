using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using com.bricksandmortarstudio.SendGridSync.Constants;
using com.bricksandmortarstudio.SendGridSync.DTO;
using com.bricksandmortarstudio.SendGridSync.Model;
using com.bricksandmortarstudio.SendGridSync.Helper;
using Newtonsoft.Json;
using Quartz;
using RestSharp;
using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;

namespace com.bricksandmortarstudio.SendGridSync.Jobs
{
    [TextField( "API Key", "The SendGrid API key.", false, "", "", order:3 )]
    [CampusField("Campus", "The campus that should be synced", true, "", false, key:"campus", order:1)]
    [IntegerField("Existing Person Update Interval", "The number of days before an existing SendGrid record should be resynced", true, 7, key: "dayInterval", order:2 )]
    [TextField("List Name", "The SendGrid")]
    [DisallowConcurrentExecution]
    public class SyncCampustoList : IJob
    {
        public void Execute( IJobExecutionContext context )
        {
            var dataMap = context.JobDetail.JobDataMap;
            string apiKey = dataMap.GetString( "APIKey" );
            string listName = dataMap.GetString("ListName");
            string campusGuid = dataMap.GetString( "campus" );

            var campus = campusGuid.AsGuidOrNull();

            //Check API Key exists and group guid is not null
            if ( string.IsNullOrWhiteSpace( apiKey ) || !campus.HasValue || string.IsNullOrWhiteSpace( listName ))
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

            var familyGuid = Rock.SystemGuid.GroupType.GROUPTYPE_FAMILY.AsGuid();
            var rockContext = new RockContext();

            var personAliasService = new PersonAliasService( rockContext );

            //TODO Fix this
            var groups = new GroupService(rockContext)
                .Queryable("GroupType,GroupType.Groups")
                .Where(gt => gt.GroupType.Guid == familyGuid);

            var campusGroups= groups
                .Where(g => g.Campus.Guid == campus.Value);

            var people = campusGroups
                .SelectMany(g => g.Members)
                .Select(gm => gm.Person)
                .Where(p => p.Email != null && p.Email != string.Empty);

            var campusAttendeesPersonAliasIds = people
                .Join( personAliasService.Queryable(), p => p.Aliases.FirstOrDefault().Id, pa => pa.Id, ( gm, pa ) => new { PersonAliasId = pa.Id } )
                .Select( x => x.PersonAliasId );

            var previouslySyncedPersonAliasIds =
                new PersonAliasHistoryService(rockContext).GetPreviouslySyncedPersonAliasIds(
                    campusAttendeesPersonAliasIds);

            var notYetSyncedIds = campusAttendeesPersonAliasIds.Except( previouslySyncedPersonAliasIds );

            var notYetSynced = personAliasService.Queryable().Where( a => notYetSyncedIds.Contains( a.Id ) );

            SyncHelper.SyncContacts( notYetSynced, apiKey );

            SyncHelper.AddPeopleToList(notYetSynced, listId.Value, apiKey);

            context.Result = "Campus synced successfully";
        }
    }

}
