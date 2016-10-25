using System;
using System.Linq;

using Rock.Data;

namespace com.bricksandmortarstudio.SendGridSync.Model
{
    /// <summary>
    /// PersonAliasHistory Service class
    /// </summary>
    public partial class PersonAliasHistoryService : Service<PersonAliasHistory>
    {
        /// <summary>
        /// Gets the PersonAliasHistory record for a given PersonAlias
        /// </summary>
        /// <param name="personAliasId">The person Alias</param>
        /// <returns></returns>
        public PersonAliasHistory GetByPersonAliasId( int personAliasId )
        {
            return Queryable().FirstOrDefault( a => a.PersonAliasId == personAliasId );
        }
    }
}
