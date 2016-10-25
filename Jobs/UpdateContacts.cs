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
    [TextField( "API Key", "The SendGrid API key.", false, "", "", 1 )]
    [IntegerField("Existing Person Update Interval", "The number of days before an existing SendGrid record should be resynced", true, 7, key: "dayInterval" )]
    [DisallowConcurrentExecution]
    public class UpdateContacts : IJob
    {
        public void Execute( IJobExecutionContext context )
        {
            var dataMap = context.JobDetail.JobDataMap;
            string apiKey = dataMap.GetString( "APIKey" );
            int dayInterval = dataMap.GetIntValue("dayInterval");

            //Check API Key exists
            if ( string.IsNullOrWhiteSpace( apiKey ) )
            {
                return;
            }

            //Check all the custom fields have been created for the SendGrid marketing campaign.
            bool fieldsExist = SyncHelper.CheckCustomFields( apiKey );
            if ( !fieldsExist )
            {
                return;
            }

            var rockContext = new RockContext();
            var previouslySyncedPersonAliasIds = new PersonAliasHistoryService( rockContext ).Queryable().AsNoTracking().Select( a => a.PersonAliasId );
            var notYetSynced = SyncHelper.FindNotYetSyncedPersonAlises( rockContext, previouslySyncedPersonAliasIds );

            var historicSyncMarker = RockDateTime.Now.AddDays(-dayInterval);
            var needReSyncPersonAliases = SyncHelper.FindResyncCandidates( rockContext, historicSyncMarker );

            int synCount = SyncHelper.SyncContacts( notYetSynced, apiKey );
            int reSyncCount = 0;
            if ( needReSyncPersonAliases.Any() )
            {
               reSyncCount = SyncHelper.SyncContacts( needReSyncPersonAliases, apiKey, true );
            }
            context.Result = string.Format( "{0} people synced for the first time, {1} people updated", synCount, reSyncCount );
        }
    }

}
