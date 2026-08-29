using Aegis.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aegis.Infrastructure.Data.Migrations;

[DbContext(typeof(AegisDbContext))]
[Migration("20260829184200_AddTimelineEventIsRead")]
public partial class AddTimelineEventIsRead : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsRead",
            table: "TimelineEvents",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsRead",
            table: "TimelineEvents");
    }
}
