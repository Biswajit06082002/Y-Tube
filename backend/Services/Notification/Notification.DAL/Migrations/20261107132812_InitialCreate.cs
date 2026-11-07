using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.DAL.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Snippet = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Link = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    ImageUrl = table.Column<string>(type: "varchar(300)", unicode: false, maxLength: 300, nullable: true),
                    DedupKey = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Unread",
                table: "Notifications",
                column: "UserId",
                filter: "[IsRead] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_DedupKey",
                table: "Notifications",
                columns: new[] { "UserId", "DedupKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_Id",
                table: "Notifications",
                columns: new[] { "UserId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");
        }
    }
}
