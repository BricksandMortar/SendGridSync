using System;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using Rock.Data;
using Rock.Model;

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

        public IQueryable<int> GetPreviouslySyncedPersonAliasIds(IQueryable<int> personAliasIds)
        {
            return Queryable()
                .AsNoTracking()
                .Where( a => personAliasIds.Contains( a.PersonAliasId ) )
                .Select( a => a.PersonAliasId );
        }
    }

    public static class Extensions
    {
        public static Expression<Func<Person, PersonAlias>> GetPersonAlias()
        {
            return person => person.Aliases.FirstOrDefault();
        }
    }
}
