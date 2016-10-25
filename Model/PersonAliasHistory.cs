using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using System.Runtime.Serialization;
using Rock.Data;
using Rock.Model;

namespace com.bricksandmortarstudio.SendGridSync.Model
{
    [Table( "_com_bricksandmortarstudio_SendGridSync_PersonAliasHistory" )]
    [DataContract]
    public class PersonAliasHistory : Model<PersonAliasHistory>, IRockEntity
    {
        [Required]
        [DataMember( IsRequired = true )]
        public int PersonAliasId { get; set; }
        
        [Required]
        [DataMember( IsRequired = true )]
        public DateTime LastUpdated { get; set; }

        #region Virtual
        [DataMember]
        public virtual PersonAlias PersonAlias { get; set; }
        #endregion
    }

    public partial class PersonAliasHistoryConfiguration : EntityTypeConfiguration<PersonAliasHistory>
    {
        public PersonAliasHistoryConfiguration()
        {
            HasRequired( p => p.PersonAlias ).WithMany().HasForeignKey( a => a.PersonAliasId ).WillCascadeOnDelete( true );
            HasEntitySetName( "PeopleAliasHistory" );
        }
    }
}
