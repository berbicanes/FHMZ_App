using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Vodostaji.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "measurements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceId = table.Column<string>(type: "text", nullable: false),
                    StationKey = table.Column<string>(type: "text", nullable: false),
                    ValueCm = table.Column<decimal>(type: "numeric", nullable: false),
                    MeasuredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Unknown"),
                    FirstFetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_measurements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "station_states",
                columns: table => new
                {
                    SourceId = table.Column<string>(type: "text", nullable: false),
                    StationKey = table.Column<string>(type: "text", nullable: false),
                    ValueCm = table.Column<decimal>(type: "numeric", nullable: true),
                    MeasuredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Unknown"),
                    StatusLabelOriginal = table.Column<string>(type: "text", nullable: false),
                    NoDataReason = table.Column<string>(type: "text", nullable: true),
                    ThresholdsJson = table.Column<string>(type: "text", nullable: true),
                    ThresholdsDefinedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_station_states", x => new { x.SourceId, x.StationKey });
                    table.CheckConstraint("ck_station_states_unknown_never_normal", "\"ValueCm\" IS NOT NULL OR \"Level\" = 'Unknown'");
                    table.CheckConstraint("ck_station_states_value_needs_time", "(\"ValueCm\" IS NULL) = (\"MeasuredAt\" IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "stations",
                columns: table => new
                {
                    SourceId = table.Column<string>(type: "text", nullable: false),
                    StationKey = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    River = table.Column<string>(type: "text", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    GaugeZero = table.Column<decimal>(type: "numeric", nullable: true),
                    ExpectedIntervalSeconds = table.Column<long>(type: "bigint", nullable: false),
                    AgencyName = table.Column<string>(type: "text", nullable: false),
                    AgencyUrl = table.Column<string>(type: "text", nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: true),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stations", x => new { x.SourceId, x.StationKey });
                });

            migrationBuilder.CreateIndex(
                name: "ix_measurements_station_time_desc",
                table: "measurements",
                columns: new[] { "SourceId", "StationKey", "MeasuredAt" },
                unique: true,
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_station_states_Level",
                table: "station_states",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_station_states_SourceId",
                table: "station_states",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_stations_SourceId",
                table: "stations",
                column: "SourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "measurements");

            migrationBuilder.DropTable(
                name: "station_states");

            migrationBuilder.DropTable(
                name: "stations");
        }
    }
}
