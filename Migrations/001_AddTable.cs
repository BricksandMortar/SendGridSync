using Rock.Plugin;

namespace com.bricksandmortarstudio.SendGridSync.Migrations
{
    [MigrationNumber( 1, "1.5.0" )]
    public class AddTable : Migration
    {
        public override void Up()
        {
            Sql( @"CREATE TABLE [_com_bricksandmortarstudio_SendGridSync_PersonAliasHistory](
    [Id] [int] not null identity(1,1),
    [PersonAliasId] [int],
	[LastUpdated] [datetime],
    [CreatedDateTime] [datetime],
    [ModifiedDateTime] [datetime],
    [CreatedByPersonAliasId] [int],
    [ModifiedByPersonAliasId] [int],
    [Guid] uniqueidentifier not null DEFAULT NEWID(),
    [ForeignKey] nvarchar(100) null,
    [ForeignGuid] uniqueidentifier null,
    [ForeignId] uniqueidentifier null
    CONSTRAINT [PK_com_bricksandmortarstudio_SendGridSync_PersonAliasHistory] PRIMARY KEY CLUSTERED ( [Id] ASC )
)
CREATE UNIQUE INDEX [IX_Guid] ON [dbo].[_com_bricksandmortarstudio_SendGridSync_PersonAliasHistory]([Guid])
CREATE INDEX [IX_CreatedByPersonAliasId] ON [dbo].[_com_bricksandmortarstudio_SendGridSync_PersonAliasHistory]([CreatedByPersonAliasId])
CREATE INDEX [IX_ModifiedByPersonAliasId] ON [dbo].[_com_bricksandmortarstudio_SendGridSync_PersonAliasHistory]([ModifiedByPersonAliasId])
CREATE INDEX [IX_ForeignKey] ON [dbo].[_com_bricksandmortarstudio_SendGridSync_PersonAliasHistory] (ForeignKey)
CREATE INDEX [IX_ForeignId] ON [dbo].[_com_bricksandmortarstudio_SendGridSync_PersonAliasHistory] (ForeignId)
CREATE INDEX [IX_ForeignGuid] ON [dbo].[_com_bricksandmortarstudio_SendGridSync_PersonAliasHistory] (ForeignGuid)" );
        }

        public override void Down()
        {
            throw new System.NotImplementedException();
        }
    }
}
