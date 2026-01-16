using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Infra.Data.Context
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<Coworking> Coworkings { get; set; }
        public DbSet<CoworkingAddress> CoworkingAddresses { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<Contract> Contracts { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<Commission> Commissions { get; set; }
        public DbSet<Transfer> Transfers { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<LocationCommission> LocationCommissions { get; set; }
        public DbSet<CoworkingPhoto> CoworkingPhotos { get; set; }
        public DbSet<CoworkingDocument> CoworkingDocuments { get; set; }
        public DbSet<CoworkingPartner> CoworkingPartners { get; set; }
        public DbSet<Language> Languages { get; set; }
        public DbSet<Translation> Translations { get; set; }
        public DbSet<SystemConfiguration> SystemConfigurations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // Role configuration
            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            });

            // UserRole configuration
            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.RoleId });
                entity.HasOne(e => e.User).WithMany(u => u.UserRoles).HasForeignKey(e => e.UserId);
                entity.HasOne(e => e.Role).WithMany().HasForeignKey(e => e.RoleId);
            });

            // Coworking configuration
            modelBuilder.Entity<Coworking>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.CNPJ).IsUnique();
                entity.Property(e => e.CNPJ).IsRequired().HasMaxLength(18);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
            });

            // CoworkingAddress configuration
            modelBuilder.Entity<CoworkingAddress>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Coworking).WithMany(c => c.Addresses).HasForeignKey(e => e.CoworkingId);
                entity.HasOne(e => e.Location).WithMany().HasForeignKey(e => e.LocationId);
                entity.HasIndex(e => new { e.CoworkingId, e.Address }).IsUnique();
            });

            // Client configuration
            modelBuilder.Entity<Client>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.CNPJ).IsUnique();
                entity.HasIndex(e => e.CPF).IsUnique();
                entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
            });

            // Contract configuration
            modelBuilder.Entity<Contract>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Client).WithMany(c => c.Contracts).HasForeignKey(e => e.ClientId);
                entity.HasOne(e => e.CoworkingAddress).WithMany().HasForeignKey(e => e.CoworkingAddressId);
                entity.HasOne(e => e.Subscription).WithOne(s => s.Contract).HasForeignKey<Subscription>(s => s.ContractId);
                entity.Property(e => e.Status).HasConversion<string>();
            });

            // Payment configuration
            modelBuilder.Entity<Payment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Subscription).WithMany(s => s.Payments).HasForeignKey(e => e.SubscriptionId);
                entity.HasOne(e => e.Validator).WithMany().HasForeignKey(e => e.ValidatedBy).OnDelete(DeleteBehavior.SetNull);
                entity.Property(e => e.Status).HasConversion<string>();
                entity.Property(e => e.Method).HasConversion<string>();
            });

            // Subscription configuration
            modelBuilder.Entity<Subscription>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Contract).WithOne(c => c.Subscription).HasForeignKey<Subscription>(s => s.ContractId);
                entity.Property(e => e.Status).HasConversion<string>();
            });

            // Commission configuration
            modelBuilder.Entity<Commission>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Payment).WithMany().HasForeignKey(e => e.PaymentId);
                entity.HasOne(e => e.Coworking).WithMany().HasForeignKey(e => e.CoworkingId);
            });

            // Transfer configuration
            modelBuilder.Entity<Transfer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Coworking).WithMany().HasForeignKey(e => e.CoworkingId);
                entity.Property(e => e.Status).HasConversion<string>();
            });

            // Document configuration
            modelBuilder.Entity<Document>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Contract).WithMany(c => c.Documents).HasForeignKey(e => e.ContractId);
                entity.Property(e => e.Type).HasConversion<string>();
            });

            // AuditLog configuration
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.ActionDate);
            });

            // Location configuration
            modelBuilder.Entity<Location>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.City).IsRequired().HasMaxLength(100);
                entity.Property(e => e.State).IsRequired().HasMaxLength(2);
            });

            // LocationCommission configuration
            modelBuilder.Entity<LocationCommission>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Location)
                    .WithMany(l => l.LocationCommissions)
                    .HasForeignKey(e => e.LocationId);
            });

            // CoworkingPhoto configuration
            modelBuilder.Entity<CoworkingPhoto>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Coworking)
                    .WithMany(c => c.Photos)
                    .HasForeignKey(e => e.CoworkingId);
            });

            // CoworkingDocument configuration
            modelBuilder.Entity<CoworkingDocument>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Coworking)
                    .WithMany(c => c.Documents)
                    .HasForeignKey(e => e.CoworkingId);
                entity.Property(e => e.Type).HasConversion<string>();
            });

            // CoworkingPartner configuration
            modelBuilder.Entity<CoworkingPartner>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Coworking)
                    .WithMany(c => c.Partners)
                    .HasForeignKey(e => e.CoworkingId);
            });

            // Language configuration
            modelBuilder.Entity<Language>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.Code).IsRequired().HasMaxLength(10);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.NativeName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.FlagIcon).HasMaxLength(50);
            });

            // Translation configuration
            modelBuilder.Entity<Translation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.Key, e.LanguageCode }).IsUnique();
                entity.Property(e => e.Key).IsRequired().HasMaxLength(255);
                entity.Property(e => e.LanguageCode).IsRequired().HasMaxLength(10);
                entity.Property(e => e.Value).IsRequired();
                entity.Property(e => e.Category).HasMaxLength(100);
            });

            // SystemConfiguration configuration
            modelBuilder.Entity<SystemConfiguration>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Key).IsUnique();
                entity.Property(e => e.Key).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Value).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
                entity.Property(e => e.UpdatedBy).HasMaxLength(255);
            });
        }
    }
}
