using Aegis.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aegis.Infrastructure.Data.Migrations;

[DbContext(typeof(AegisDbContext))]
[Migration("20260826020000_AddRssFeedDefaultRegion")]
public partial class AddRssFeedDefaultRegion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DefaultRegionQuery",
            table: "RssFeeds",
            type: "TEXT",
            maxLength: 200,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DefaultRegionQuery",
            table: "RssFeeds");
    }
}
