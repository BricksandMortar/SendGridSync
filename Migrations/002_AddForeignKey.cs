using Rock.Plugin;

namespace com.bricksandmortarstudio.SendGridSync.Migrations
{
    [MigrationNumber( 2, "1.5.0" )]
    public class AddForeignKey : Migration
    {
        public override void Up()
        {
            Sql( @"ALTER TABLE [_com_bricksandmortarstudio_SendGridSync_PersonAliasHistory]
ADD FOREIGN KEY ([PersonAliasId])
REFERENCES [PersonAlias]([Id])" );
        }

        public override void Down()
        {
            throw new System.NotImplementedException();
        }
    }
}
