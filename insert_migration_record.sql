-- Insert migration record to prevent the InitialCreate migration from running
-- since tables already exist in the database

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES ('20251127154710_InitialCreate', '8.0.0');