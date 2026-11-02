using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Video.DAL.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VideoLikes",
                columns: table => new
                {
                    VideoId = table.Column<string>(type: "varchar(11)", unicode: false, maxLength: 11, nullable: false),
                    UserId = table.Column<string>(type: "varchar(36)", unicode: false, maxLength: 36, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoLikes", x => new { x.VideoId, x.UserId });
                });

            migrationBuilder.CreateTable(
                name: "Videos",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(11)", unicode: false, maxLength: 11, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ChannelId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ChannelName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Views = table.Column<long>(type: "bigint", nullable: false),
                    BaseLikes = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ProcessingProgress = table.Column<int>(type: "int", nullable: false),
                    ProcessingError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    Renditions = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourcePath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    ThumbnailPath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    MasterPlaylistPath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Videos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VideoTags",
                columns: table => new
                {
                    VideoId = table.Column<string>(type: "varchar(11)", unicode: false, maxLength: 11, nullable: false),
                    Tag = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoTags", x => new { x.VideoId, x.Tag });
                    table.ForeignKey(
                        name: "FK_VideoTags_Videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoLikes_UserId",
                table: "VideoLikes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Videos_OwnerId",
                table: "Videos",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Videos_Status_Category_PublishedAtUtc",
                table: "Videos",
                columns: new[] { "Status", "Category", "PublishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Videos_Status_PublishedAtUtc",
                table: "Videos",
                columns: new[] { "Status", "PublishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoTags_Tag",
                table: "VideoTags",
                column: "Tag");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoLikes");

            migrationBuilder.DropTable(
                name: "VideoTags");

            migrationBuilder.DropTable(
                name: "Videos");
        }
    }
}
