using ChatApp.Data;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Services;

public class DbSchemaInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    public DbSchemaInitializer(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.UserSettings','NotifyMessages') IS NULL ALTER TABLE dbo.UserSettings ADD NotifyMessages bit NOT NULL CONSTRAINT DF_UserSettings_NotifyMessages DEFAULT 1;
IF COL_LENGTH('dbo.UserSettings','NotifyCalls') IS NULL ALTER TABLE dbo.UserSettings ADD NotifyCalls bit NOT NULL CONSTRAINT DF_UserSettings_NotifyCalls DEFAULT 1;
IF COL_LENGTH('dbo.UserSettings','NotifyMeetings') IS NULL ALTER TABLE dbo.UserSettings ADD NotifyMeetings bit NOT NULL CONSTRAINT DF_UserSettings_NotifyMeetings DEFAULT 1;
IF COL_LENGTH('dbo.UserSettings','NotifyFriendRequests') IS NULL ALTER TABLE dbo.UserSettings ADD NotifyFriendRequests bit NOT NULL CONSTRAINT DF_UserSettings_NotifyFriendRequests DEFAULT 1;
IF COL_LENGTH('dbo.UserSettings','NotifyGroups') IS NULL ALTER TABLE dbo.UserSettings ADD NotifyGroups bit NOT NULL CONSTRAINT DF_UserSettings_NotifyGroups DEFAULT 1;
IF COL_LENGTH('dbo.UserSettings','NotifySounds') IS NULL ALTER TABLE dbo.UserSettings ADD NotifySounds bit NOT NULL CONSTRAINT DF_UserSettings_NotifySounds DEFAULT 1;
IF COL_LENGTH('dbo.UserSettings','NotifyBrowser') IS NULL ALTER TABLE dbo.UserSettings ADD NotifyBrowser bit NOT NULL CONSTRAINT DF_UserSettings_NotifyBrowser DEFAULT 1;
IF COL_LENGTH('dbo.UserSettings','Theme') IS NULL ALTER TABLE dbo.UserSettings ADD Theme nvarchar(20) NOT NULL CONSTRAINT DF_UserSettings_Theme DEFAULT 'system';
IF COL_LENGTH('dbo.UserSettings','AccentColor') IS NULL ALTER TABLE dbo.UserSettings ADD AccentColor nvarchar(20) NOT NULL CONSTRAINT DF_UserSettings_AccentColor DEFAULT '#2563eb';
IF COL_LENGTH('dbo.UserSettings','UiDensity') IS NULL ALTER TABLE dbo.UserSettings ADD UiDensity nvarchar(20) NOT NULL CONSTRAINT DF_UserSettings_UiDensity DEFAULT 'comfortable';
IF COL_LENGTH('dbo.UserSettings','ReduceMotion') IS NULL ALTER TABLE dbo.UserSettings ADD ReduceMotion bit NOT NULL CONSTRAINT DF_UserSettings_ReduceMotion DEFAULT 0;
IF OBJECT_ID('dbo.ChatGroups','U') IS NULL CREATE TABLE dbo.ChatGroups (Id uniqueidentifier NOT NULL PRIMARY KEY, Name nvarchar(120) NOT NULL, Description nvarchar(500) NULL, PhotoUrl nvarchar(2048) NULL, OwnerId nvarchar(450) NOT NULL, CreatedAt datetime2 NOT NULL, CONSTRAINT FK_ChatGroups_AspNetUsers_OwnerId FOREIGN KEY(OwnerId) REFERENCES dbo.AspNetUsers(Id));
IF OBJECT_ID('dbo.GroupMembers','U') IS NULL CREATE TABLE dbo.GroupMembers (Id uniqueidentifier NOT NULL PRIMARY KEY, GroupId uniqueidentifier NOT NULL, UserId nvarchar(450) NOT NULL, Role int NOT NULL, JoinedAt datetime2 NOT NULL, CONSTRAINT FK_GroupMembers_ChatGroups_GroupId FOREIGN KEY(GroupId) REFERENCES dbo.ChatGroups(Id) ON DELETE CASCADE, CONSTRAINT FK_GroupMembers_AspNetUsers_UserId FOREIGN KEY(UserId) REFERENCES dbo.AspNetUsers(Id));
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_GroupMembers_GroupId_UserId') CREATE UNIQUE INDEX IX_GroupMembers_GroupId_UserId ON dbo.GroupMembers(GroupId,UserId);
IF OBJECT_ID('dbo.GroupInvites','U') IS NULL CREATE TABLE dbo.GroupInvites (Id uniqueidentifier NOT NULL PRIMARY KEY, GroupId uniqueidentifier NOT NULL, InvitedUserId nvarchar(450) NOT NULL, InvitedById nvarchar(450) NOT NULL, Status int NOT NULL, CreatedAt datetime2 NOT NULL, CONSTRAINT FK_GroupInvites_ChatGroups_GroupId FOREIGN KEY(GroupId) REFERENCES dbo.ChatGroups(Id) ON DELETE CASCADE, CONSTRAINT FK_GroupInvites_AspNetUsers_InvitedUserId FOREIGN KEY(InvitedUserId) REFERENCES dbo.AspNetUsers(Id), CONSTRAINT FK_GroupInvites_AspNetUsers_InvitedById FOREIGN KEY(InvitedById) REFERENCES dbo.AspNetUsers(Id));
IF OBJECT_ID('dbo.GroupMessages','U') IS NULL CREATE TABLE dbo.GroupMessages (Id bigint IDENTITY(1,1) NOT NULL PRIMARY KEY, GroupId uniqueidentifier NOT NULL, SenderId nvarchar(450) NOT NULL, Content nvarchar(10000) NULL, Type int NOT NULL, IsDeleted bit NOT NULL, SentAt datetime2 NOT NULL, CONSTRAINT FK_GroupMessages_ChatGroups_GroupId FOREIGN KEY(GroupId) REFERENCES dbo.ChatGroups(Id) ON DELETE CASCADE, CONSTRAINT FK_GroupMessages_AspNetUsers_SenderId FOREIGN KEY(SenderId) REFERENCES dbo.AspNetUsers(Id));
IF OBJECT_ID('dbo.Statuses','U') IS NULL CREATE TABLE dbo.Statuses (Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, UserId nvarchar(450) NOT NULL, Text nvarchar(1000) NULL, MediaUrl nvarchar(2048) NULL, MediaType nvarchar(20) NOT NULL, CreatedAt datetime2 NOT NULL, ExpiresAt datetime2 NOT NULL, CONSTRAINT FK_Statuses_AspNetUsers_UserId FOREIGN KEY(UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE);
IF OBJECT_ID('dbo.StatusViews','U') IS NULL CREATE TABLE dbo.StatusViews (Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, StatusId int NOT NULL, UserId nvarchar(450) NOT NULL, ViewedAt datetime2 NOT NULL, CONSTRAINT FK_StatusViews_Statuses_StatusId FOREIGN KEY(StatusId) REFERENCES dbo.Statuses(Id) ON DELETE CASCADE, CONSTRAINT FK_StatusViews_AspNetUsers_UserId FOREIGN KEY(UserId) REFERENCES dbo.AspNetUsers(Id));
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_StatusViews_StatusId_UserId') CREATE UNIQUE INDEX IX_StatusViews_StatusId_UserId ON dbo.StatusViews(StatusId,UserId);
", cancellationToken: cancellationToken);
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
