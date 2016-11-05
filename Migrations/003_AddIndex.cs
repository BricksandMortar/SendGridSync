using Rock.Plugin;

namespace com.bricksandmortarstudio.SendGridSync.Migrations
{
    [MigrationNumber( 3, "1.5.0" )]
    public class AddIndex : Migration
    {
        public override void Up()
        {
            Sql( @"CREATE INDEX IX_PersonAliasId
                    ON [_com_bricksandmortarstudio_SendGridSync_PersonAliasHistory] ([PersonAliasId])" );
        }

        public override void Down()
        {
            throw new System.NotImplementedException();
        }
    }
}
