using Rock.Data;

namespace com.bricksandmortarstudio.SendGridSync.Model
{
    /// <summary>
    /// PersonAliasHistory Service class
    /// </summary>
    public partial class PersonAliasHistoryService : Service<PersonAliasHistory>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PersonAliasHistoryService"/> class
        /// </summary>
        /// <param name="context">The context.</param>
        public PersonAliasHistoryService(RockContext context) : base(context)
        {
        }

        /// <summary>
        /// Determines whether this instance can delete the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="errorMessage">The error message.</param>
        /// <returns>
        ///   <c>true</c> if this instance can delete the specified item; otherwise, <c>false</c>.
        /// </returns>
        public bool CanDelete( PersonAliasHistory item, out string errorMessage )
        {
            errorMessage = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Generated Extension Methods
    /// </summary>
    public static class PersonAliasHistoryExtensionMethods
    {
        /// <summary>
        /// Clones this PersonAliasHistory object to a new PersonAliasHistory object
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="deepCopy">if set to <c>true</c> a deep copy is made. If false, only the basic entity properties are copied.</param>
        /// <returns></returns>
        public static PersonAliasHistory Clone( this PersonAliasHistory source, bool deepCopy )
        {
            if (deepCopy)
            {
                return source.Clone() as PersonAliasHistory;
            }
            else
            {
                var target = new PersonAliasHistory();
                target.CopyPropertiesFrom( source );
                return target;
            }
        }

        /// <summary>
        /// Copies the properties from another PersonAliasHistory object to this PersonAliasHistory object
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="source">The source.</param>
        public static void CopyPropertiesFrom( this PersonAliasHistory target, PersonAliasHistory source )
        {
            target.Id = source.Id;
            target.ForeignGuid = source.ForeignGuid;
            target.ForeignKey = source.ForeignKey;
            target.LastUpdated = source.LastUpdated;
            target.PersonAliasId = source.PersonAliasId;
            target.CreatedDateTime = source.CreatedDateTime;
            target.ModifiedDateTime = source.ModifiedDateTime;
            target.CreatedByPersonAliasId = source.CreatedByPersonAliasId;
            target.ModifiedByPersonAliasId = source.ModifiedByPersonAliasId;
            target.Guid = source.Guid;
            target.ForeignId = source.ForeignId;

        }
    }
}
