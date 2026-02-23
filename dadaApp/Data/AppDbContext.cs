using Microsoft.EntityFrameworkCore;
using dadaApp.Models;

namespace dadaApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        // Tables existantes
        public DbSet<User> Users { get; set; }
        
        // Tables comptabilité
        public DbSet<Compte> Comptes { get; set; }
        public DbSet<Ecriture> Ecritures { get; set; }
        public DbSet<LettrageManuel> LettragesManuels { get; set; }
        
        // Vue
        public DbSet<VueSoldeCompte> VueSoldesComptes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuration Compte
            modelBuilder.Entity<Compte>(entity =>
            {
                entity.ToTable("Comptes");
                
                entity.HasKey(e => e.CompteId);
                
                entity.HasIndex(e => new { e.NumeroCompte, e.CodeClient })
                    .IsUnique();
                
                entity.HasMany(e => e.Ecritures)
                    .WithOne(e => e.Compte)
                    .HasForeignKey(e => e.CompteId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configuration Ecriture
            modelBuilder.Entity<Ecriture>(entity =>
            {
                entity.ToTable("Ecritures");
                
                entity.HasKey(e => e.EcritureId);
                
                entity.HasOne(e => e.Compte)
                    .WithMany(c => c.Ecritures)
                    .HasForeignKey(e => e.CompteId);
            });

            // Configuration Vue (lecture seule)
            modelBuilder.Entity<VueSoldeCompte>(entity =>
            {
                entity.ToView("VueSoldesComptes");
                entity.HasNoKey();
            });

            // Conversion automatique DateTime en UTC pour PostgreSQL
            var dateTimeConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
                v => v.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : v.ToUniversalTime(),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            var nullableDateTimeConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
                v => !v.HasValue ? v : (v.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v.Value.ToUniversalTime()),
                v => !v.HasValue ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc));

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (entityType.IsKeyless) continue;

                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime))
                    {
                        property.SetValueConverter(dateTimeConverter);
                    }
                    else if (property.ClrType == typeof(DateTime?))
                    {
                        property.SetValueConverter(nullableDateTimeConverter);
                    }
                }
            }
        }
    }
}