using Microsoft.EntityFrameworkCore;
using Vodostaji.Core;

namespace Vodostaji.Data;

public class VodostajiDbContext(DbContextOptions<VodostajiDbContext> options) : DbContext(options)
{
    public DbSet<StationRow> Stations => Set<StationRow>();

    public DbSet<StationStateRow> StationStates => Set<StationStateRow>();

    public DbSet<MeasurementRow> Measurements => Set<MeasurementRow>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasPostgresExtension("postgis");

        builder.Entity<StationRow>(entity =>
        {
            entity.ToTable("stations");
            entity.HasKey(e => new { e.SourceId, e.StationKey });
            entity.Property(e => e.GaugeZero).HasColumnType("numeric");
            entity.HasIndex(e => e.SourceId);
        });

        builder.Entity<StationStateRow>(entity =>
        {
            entity.ToTable("station_states");
            entity.HasKey(e => new { e.SourceId, e.StationKey });

            // `numeric` bez zadate preciznosti. H_CM stiže sa artefaktima jednostruke
            // preciznosti (`17.7000008`) i baza ih mora primiti doslovno — zaokruživanje
            // na putu do diska bi bilo tiho mijenjanje tuđeg mjerenja.
            entity.Property(e => e.ValueCm).HasColumnType("numeric");

            // Stupanj se čuva kao tekst, ne kao broj. Čitljiv je u bazi, preživi preuređenje
            // enuma, a podrazumijevana vrijednost je `Unknown` — red koji neko upiše bez
            // stupnja ispada nepoznat, nikad normalan.
            entity.Property(e => e.Level)
                .HasConversion<string>()
                .HasMaxLength(16)
                .HasDefaultValue(AlertLevel.Unknown);

            entity.HasIndex(e => e.SourceId);
            entity.HasIndex(e => e.Level);

            // Zlatno pravilo 1, upisano u shemu. Nije komentar nego ograničenje: red bez
            // vrijednosti ne može nositi nijedan stupanj osim `Unknown`. Ni greška u kodu,
            // ni ručni UPDATE, ni migracija ne mogu od nepoznatog napraviti normalno.
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_station_states_unknown_never_normal",
                "\"ValueCm\" IS NOT NULL OR \"Level\" = 'Unknown'"));

            // Vrijednost i vrijeme mjerenja idu zajedno ili nikako — vrijednost bez vremena
            // se ne može pošteno prikazati (zlatno pravilo 2).
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_station_states_value_needs_time",
                "(\"ValueCm\" IS NULL) = (\"MeasuredAt\" IS NULL)"));
        });

        builder.Entity<MeasurementRow>(entity =>
        {
            entity.ToTable("measurements");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ValueCm).HasColumnType("numeric");

            entity.Property(e => e.Level)
                .HasConversion<string>()
                .HasMaxLength(16)
                .HasDefaultValue(AlertLevel.Unknown);

            // Ingest je idempotentan zahvaljujući ovom indeksu. Izvor se osvježava na sat,
            // a pitamo ga svakih 15 minuta — bez njega bi isti podatak ušao četiri puta i
            // graf bi imao stepenice kojih u rijeci nema.
            entity.HasIndex(e => new { e.SourceId, e.StationKey, e.MeasuredAt }).IsUnique();

            // Graf 7/30 dana čita po stanici, unazad po vremenu.
            entity.HasIndex(e => new { e.SourceId, e.StationKey, e.MeasuredAt })
                .HasDatabaseName("ix_measurements_station_time_desc")
                .IsDescending(false, false, true);
        });
    }
}
