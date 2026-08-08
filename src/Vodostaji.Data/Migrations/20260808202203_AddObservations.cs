using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Vodostaji.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddObservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "observations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceId = table.Column<string>(type: "text", nullable: false),
                    StationKey = table.Column<string>(type: "text", nullable: false),
                    Parameter = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Unknown"),
                    ParameterLabelOriginal = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Value = table.Column<decimal>(type: "numeric", nullable: false),
                    Unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MeasuredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FirstFetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_observations", x => x.Id);
                    table.CheckConstraint("ck_observations_no_water_level", "\"Parameter\" <> 'WaterLevel'");
                });

            migrationBuilder.CreateIndex(
                name: "ix_observations_station_param_time_desc",
                table: "observations",
                columns: new[] { "SourceId", "StationKey", "Parameter", "MeasuredAt" },
                unique: true,
                descending: new[] { false, false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "observations");
        }
    }
}
