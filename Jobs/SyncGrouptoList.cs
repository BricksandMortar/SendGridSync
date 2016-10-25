using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using com.bricksandmortarstudio.SendGridSync.Constants;
using com.bricksandmortarstudio.SendGridSync.DTO;
using com.bricksandmortarstudio.SendGridSync.Model;
using com.bricksandmortarstudio.SendGridSync.Utils;
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
            int dayInterval = dataMap.GetIntValue("dayInterval");
            string groupGuid = dataMap.GetString("group");

            var group = groupGuid.AsGuidOrNull();

            //Check API Key exists and group guid is not null
            if ( string.IsNullOrWhiteSpace( apiKey ) || !group.HasValue  )
            {
                return;
            }

            //Check all the custom fields have been created for the SendGrid marketing campaign.
            bool fieldsExist = SendGridRequestUtil.CheckCustomFields( apiKey );
            if ( !fieldsExist )
            {
                return;
            }


            var rockContext = new RockContext();
            var groupMembers = new GroupMemberService(rockContext)
                .Queryable("Group,Person")
                .Where(a => a.Group.Guid == group)
                .Select(p => p.Person);

            var groupMemberAliasIds = groupMembers.Select(a => a.PrimaryAliasId);

            var previouslySyncedPersonAliasIds = new PersonAliasHistoryService( rockContext )
                .Queryable()
                .Where(a => groupMemberAliasIds.Contains(a.PersonAliasId) )
                .AsNoTracking()
                .Select( a => a.PersonAliasId );

            var notYetSynced = groupMembers
                .Where(g => g.PrimaryAliasId.HasValue && !previouslySyncedPersonAliasIds.Contains(g.PrimaryAliasId.Value))
                .Select(p => p.PrimaryAlias)
                .ToList();
            
            SendGridRequestUtil.SyncContacts( notYetSynced, apiKey );

            context.Result = string.Format( "{0} people synced for the first time, {1} people updated", synCount, reSyncCount );
        }
    }

}
