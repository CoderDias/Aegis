using Aegis.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aegis.Infrastructure.Data.Migrations;

[DbContext(typeof(AegisDbContext))]
[Migration("20260827040000_AddHostVulnerabilities")]
public partial class AddHostVulnerabilities : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "VulnerabilitiesJson",
            table: "DiscoveredHosts",
            type: "TEXT",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "VulnerabilitiesJson",
            table: "DiscoveredHosts");
    }
}
