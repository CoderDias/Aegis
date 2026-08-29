using Aegis.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aegis.Infrastructure.Data.Migrations;

[DbContext(typeof(AegisDbContext))]
[Migration("20260826013000_AddDiscoveredHostCache")]
public partial class AddDiscoveredHostCache : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CensysApiUsage",
            columns: table => new
            {
                MonthKey = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                QueryCount = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_CensysApiUsage", x => x.MonthKey));

        migrationBuilder.CreateTable(
            name: "CountryIngestStates",
            columns: table => new
            {
                CountryCode = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                CidrCursor = table.Column<int>(type: "INTEGER", nullable: false),
                SearchPageToken = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                SearchComplete = table.Column<bool>(type: "INTEGER", nullable: false),
                UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_CountryIngestStates", x => x.CountryCode));

        migrationBuilder.CreateTable(
            name: "DiscoveredHosts",
            columns: table => new
            {
                Ip = table.Column<string>(type: "TEXT", maxLength: 45, nullable: false),
                CountryCode = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                Lat = table.Column<double>(type: "REAL", nullable: true),
                Lng = table.Column<double>(type: "REAL", nullable: true),
                City = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                Country = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                Org = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                Product = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                Port = table.Column<int>(type: "INTEGER", nullable: true),
                Transport = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                Source = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                IsUp = table.Column<bool>(type: "INTEGER", nullable: true),
                LastProbeAt = table.Column<string>(type: "TEXT", nullable: true),
                CensysFetchedAt = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_DiscoveredHosts", x => x.Ip));

        migrationBuilder.CreateIndex(
            name: "IX_DiscoveredHosts_CensysFetchedAt",
            table: "DiscoveredHosts",
            column: "CensysFetchedAt");

        migrationBuilder.CreateIndex(
            name: "IX_DiscoveredHosts_CountryCode_Lat_Lng",
            table: "DiscoveredHosts",
            columns: new[] { "CountryCode", "Lat", "Lng" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CensysApiUsage");
        migrationBuilder.DropTable(name: "CountryIngestStates");
        migrationBuilder.DropTable(name: "DiscoveredHosts");
    }
}
