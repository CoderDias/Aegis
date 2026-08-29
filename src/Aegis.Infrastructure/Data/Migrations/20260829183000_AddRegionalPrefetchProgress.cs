using Aegis.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aegis.Infrastructure.Data.Migrations;

[DbContext(typeof(AegisDbContext))]
[Migration("20260829183000_AddRegionalPrefetchProgress")]
public partial class AddRegionalPrefetchProgress : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "OverpassTileIndex",
            table: "CountryIngestStates",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<bool>(
            name: "OverpassWarmComplete",
            table: "CountryIngestStates",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "ShodanRegionIndex",
            table: "CountryIngestStates",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<bool>(
            name: "ShodanWarmComplete",
            table: "CountryIngestStates",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "PrefetchWarmComplete",
            table: "CountryIngestStates",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "LastPrefetchUtc",
            table: "CountryIngestStates",
            type: "TEXT",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "OverpassTileIndex", table: "CountryIngestStates");
        migrationBuilder.DropColumn(name: "OverpassWarmComplete", table: "CountryIngestStates");
        migrationBuilder.DropColumn(name: "ShodanRegionIndex", table: "CountryIngestStates");
        migrationBuilder.DropColumn(name: "ShodanWarmComplete", table: "CountryIngestStates");
        migrationBuilder.DropColumn(name: "PrefetchWarmComplete", table: "CountryIngestStates");
        migrationBuilder.DropColumn(name: "LastPrefetchUtc", table: "CountryIngestStates");
    }
}
