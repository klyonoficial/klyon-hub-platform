using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Domain.Entities;
using System.Reflection;
using System;

namespace KlyonHubPlatform.Infra.Data.Context
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                return;
            }
            
            base.OnConfiguring(optionsBuilder);
        }

        public override int SaveChanges()
        {
            AdjustDateTimeProperties();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            AdjustDateTimeProperties();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void AdjustDateTimeProperties()
        {
            // Ajustar todas as datas para horário de Brasília antes de salvar
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                var entityType = entry.Entity.GetType();
                var properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (var property in properties)
                {
                    // Processar propriedades DateTime (não nullable)
                    if (property.PropertyType == typeof(DateTime))
                    {
                        var currentValue = (DateTime)property.GetValue(entry.Entity)!;
                        
                        // Se for uma nova entidade e a propriedade está com valor default
                        if (entry.State == EntityState.Added && currentValue == default(DateTime))
                        {
                            // Propriedades de data/hora de criação devem receber horário de Brasília
                            if (property.Name.Contains("CreatedAt", StringComparison.OrdinalIgnoreCase) ||
                                property.Name.Contains("ActionAt", StringComparison.OrdinalIgnoreCase) ||
                                property.Name.Contains("GeneratedAt", StringComparison.OrdinalIgnoreCase) ||
                                property.Name.Contains("AssignedAt", StringComparison.OrdinalIgnoreCase) ||
                                property.Name.Contains("EffectiveDate", StringComparison.OrdinalIgnoreCase) ||
                                property.Name.Contains("ActionDate", StringComparison.OrdinalIgnoreCase))
                            {
                                property.SetValue(entry.Entity, GetBrazilianTime());
                            }
                        }
                        // Se o valor está em UTC, converter para horário de Brasília
                        else if (currentValue != default(DateTime) && currentValue.Kind == DateTimeKind.Utc)
                        {
                            property.SetValue(entry.Entity, ConvertToBrazilianTime(currentValue));
                        }
                        // Se não tem Kind especificado, assumir que já está no horário correto (Brasília)
                        // Não converter para evitar dupla conversão
                        else if (currentValue != default(DateTime) && currentValue.Kind == DateTimeKind.Unspecified)
                        {
                            // Manter como está, apenas garantir que seja Unspecified
                            property.SetValue(entry.Entity, DateTime.SpecifyKind(currentValue, DateTimeKind.Unspecified));
                        }
                    }
                    // Processar propriedades DateTime? (nullable)
                    else if (property.PropertyType == typeof(DateTime?))
                    {
                        var currentValue = (DateTime?)property.GetValue(entry.Entity);
                        
                        if (currentValue.HasValue)
                        {
                            // Se está em UTC, converter para horário de Brasília
                            if (currentValue.Value.Kind == DateTimeKind.Utc)
                            {
                                property.SetValue(entry.Entity, ConvertToBrazilianTime(currentValue.Value));
                            }
                            // Se não tem Kind especificado, assumir que já está no horário correto (Brasília)
                            // Não converter para evitar dupla conversão
                            else if (currentValue.Value.Kind == DateTimeKind.Unspecified)
                            {
                                // Manter como está, apenas garantir que seja Unspecified
                                property.SetValue(entry.Entity, DateTime.SpecifyKind(currentValue.Value, DateTimeKind.Unspecified));
                            }
                        }
                    }
                }
            }
        }

        private static DateTime GetBrazilianTime()
        {
            try
            {
                // Obter horário atual de Brasília
                var brazilTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
                var utcNow = DateTime.UtcNow;
                var brazilianTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, brazilTimeZone);
                // Marcar como Unspecified para que o EF Core salve o valor exatamente como está, sem conversão
                return DateTime.SpecifyKind(brazilianTime, DateTimeKind.Unspecified);
            }
            catch
            {
                // Fallback: se não conseguir obter o timezone, usar UTC-3 manualmente
                var brazilianTime = DateTime.UtcNow.AddHours(-3);
                return DateTime.SpecifyKind(brazilianTime, DateTimeKind.Unspecified);
            }
        }

        private static DateTime ConvertToBrazilianTime(DateTime utcDateTime)
        {
            try
            {
                var brazilTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
                DateTime brazilianTime;
                
                if (utcDateTime.Kind == DateTimeKind.Unspecified)
                {
                    // Assumir que é UTC se não especificado
                    brazilianTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), brazilTimeZone);
                }
                else if (utcDateTime.Kind == DateTimeKind.Utc)
                {
                    brazilianTime = TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, brazilTimeZone);
                }
                else
                {
                    // Se já está em horário local, assumir que já é horário de Brasília
                    brazilianTime = utcDateTime;
                }
                
                // Marcar como Unspecified para que o EF Core não faça conversão adicional
                return DateTime.SpecifyKind(brazilianTime, DateTimeKind.Unspecified);
            }
            catch
            {
                // Fallback: subtrair 3 horas se não conseguir converter
                var brazilianTime = utcDateTime.Kind == DateTimeKind.Utc 
                    ? utcDateTime.AddHours(-3) 
                    : utcDateTime;
                return DateTime.SpecifyKind(brazilianTime, DateTimeKind.Unspecified);
            }
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
        public DbSet<CreditCard> CreditCards { get; set; }
        public DbSet<AddressDeclaration> AddressDeclarations { get; set; }
        public DbSet<Interest> Interests { get; set; }
        public DbSet<CoworkingProduct> CoworkingProducts { get; set; }
        public DbSet<CoworkingRating> CoworkingRatings { get; set; }
        public DbSet<ClientCredit> ClientCredits { get; set; }
        public DbSet<CreditPlan> CreditPlans { get; set; }
        public DbSet<CreditPurchase> CreditPurchases { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<ReservationCommission> ReservationCommissions { get; set; }
        public DbSet<PlatformCreditBalance> PlatformCreditBalances { get; set; }
        public DbSet<CoworkingRoom> CoworkingRooms { get; set; }
        public DbSet<CoworkingRoomPhoto> CoworkingRoomPhotos { get; set; }
        public DbSet<CoworkingOperatingHours> CoworkingOperatingHours { get; set; }
        public DbSet<BlockedDate> BlockedDates { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<PartnerService> PartnerServices { get; set; }
        public DbSet<SefazConfiguration> SefazConfigurations { get; set; }
        public DbSet<CoworkingSefazConfiguration> CoworkingSefazConfigurations { get; set; }
        public DbSet<FiscalNote> FiscalNotes { get; set; }
        public DbSet<FiscalNoteScriptExecution> FiscalNoteScriptExecutions { get; set; }
        public DbSet<PaymentHistory> PaymentHistories { get; set; }
        public DbSet<CapacityChangeRequest> CapacityChangeRequests { get; set; }

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
                // Removido HasDefaultValueSql - a data será definida automaticamente pelo AdjustDateTimeProperties
                // entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
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
                entity.Property(e => e.ApprovalStatus).HasConversion<string>().HasMaxLength(50);
                entity.Property(e => e.RejectionReason).HasMaxLength(2000);
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

            // CreditCard configuration
            modelBuilder.Entity<CreditCard>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Client).WithMany().HasForeignKey(e => e.ClientId);
                entity.HasIndex(e => new { e.ClientId, e.LastFourDigits, e.ExpiryMonth, e.ExpiryYear }).IsUnique();
            });

            // Contract configuration
            modelBuilder.Entity<Contract>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Client).WithMany(c => c.Contracts).HasForeignKey(e => e.ClientId);
                entity.HasOne(e => e.CoworkingAddress).WithMany().HasForeignKey(e => e.CoworkingAddressId);
                entity.HasOne(e => e.Subscription).WithOne(s => s.Contract).HasForeignKey<Subscription>(s => s.ContractId);
                entity.HasOne(e => e.AssistedSaleAdmin).WithMany().HasForeignKey(e => e.AssistedSaleAdminId).OnDelete(DeleteBehavior.SetNull);
                entity.Property(e => e.Status).HasConversion<string>();
            });

            // Payment configuration
            modelBuilder.Entity<Payment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Subscription).WithMany(s => s.Payments).HasForeignKey(e => e.SubscriptionId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(e => e.Reservation).WithMany().HasForeignKey(e => e.ReservationId).OnDelete(DeleteBehavior.SetNull);
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
                entity.Property(e => e.Id).ValueGeneratedOnAdd(); // Garantir auto-incremento
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.ActionDate);
                entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.SetNull);
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
                entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
                entity.Property(e => e.RejectionReason).HasMaxLength(2000);
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

            // AddressDeclaration configuration
            modelBuilder.Entity<AddressDeclaration>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Contract)
                    .WithMany()
                    .HasForeignKey(e => e.ContractId);
                entity.HasIndex(e => e.DeclarationNumber).IsUnique();
                entity.HasIndex(e => new { e.ContractId, e.Version });
                entity.Property(e => e.DeclarationNumber).IsRequired().HasMaxLength(50);
            });

            // Interest configuration
            modelBuilder.Entity<Interest>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.CoworkingAddress)
                    .WithMany()
                    .HasForeignKey(e => e.CoworkingAddressId);
                entity.HasOne(e => e.ContactedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.ContactedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Phone).HasMaxLength(50);
                entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("Pending");
                entity.Property(e => e.StartTime).HasColumnType("TIME");
                entity.Property(e => e.EndTime).HasColumnType("TIME");
                // Removido HasDefaultValueSql - a data será definida automaticamente pelo AdjustDateTimeProperties
                // entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // CoworkingProduct configuration
            modelBuilder.Entity<CoworkingProduct>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Coworking)
                    .WithMany(c => c.Products)
                    .HasForeignKey(e => e.CoworkingId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.Property(e => e.ProductType).IsRequired().HasMaxLength(50);
                entity.HasIndex(e => new { e.CoworkingId, e.ProductType }).IsUnique();
                // Removido HasDefaultValueSql - a data será definida automaticamente pelo AdjustDateTimeProperties
                // entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // CoworkingRating configuration
            modelBuilder.Entity<CoworkingRating>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Coworking)
                    .WithMany()
                    .HasForeignKey(e => e.CoworkingId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Client)
                    .WithMany()
                    .HasForeignKey(e => e.ClientId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.Property(e => e.Rating).IsRequired();
                entity.HasCheckConstraint("CK_Rating_Range", "Rating >= 1 AND Rating <= 5");
                entity.Property(e => e.Comment).HasMaxLength(1000);
                // Removido HasDefaultValueSql - a data será definida automaticamente pelo AdjustDateTimeProperties
                // entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // ClientCredit configuration
            modelBuilder.Entity<ClientCredit>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Client)
                    .WithMany()
                    .HasForeignKey(e => e.ClientId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.Property(e => e.Credits).IsRequired();
                // Removido HasDefaultValueSql - a data será definida automaticamente pelo AdjustDateTimeProperties
                // entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // CreditPlan configuration
            modelBuilder.Entity<CreditPlan>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Price).IsRequired().HasColumnType("DECIMAL(10,2)");
                entity.Property(e => e.Credits).IsRequired();
                entity.Property(e => e.Period).IsRequired().HasMaxLength(50);
                // Removido HasDefaultValueSql - a data será definida automaticamente pelo AdjustDateTimeProperties
                // entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // CreditPurchase configuration
            modelBuilder.Entity<CreditPurchase>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Client)
                    .WithMany()
                    .HasForeignKey(e => e.ClientId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.CreditPlan)
                    .WithMany()
                    .HasForeignKey(e => e.CreditPlanId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.Property(e => e.AmountPaid).IsRequired().HasColumnType("DECIMAL(10,2)");
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Pending");
                entity.Property(e => e.PaymentMethod).HasConversion<string>();
                entity.Property(e => e.ExternalPaymentId).HasMaxLength(255);
                entity.Property(e => e.PixCode).HasMaxLength(500);
                entity.Property(e => e.BoletoCode).HasMaxLength(100);
                entity.Property(e => e.BoletoBarcode).HasMaxLength(100);
                entity.Property(e => e.TransactionId).HasMaxLength(255);
                // Removido HasDefaultValueSql - a data será definida automaticamente pelo AdjustDateTimeProperties
                // entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // Reservation configuration
            modelBuilder.Entity<Reservation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Client)
                    .WithMany()
                    .HasForeignKey(e => e.ClientId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.CoworkingAddress)
                    .WithMany()
                    .HasForeignKey(e => e.CoworkingAddressId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.CoworkingRoom)
                    .WithMany()
                    .HasForeignKey(e => e.CoworkingRoomId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.Property(e => e.ReservationDate).IsRequired();
                entity.Property(e => e.StartTime).HasColumnType("TIME");
                entity.Property(e => e.EndTime).HasColumnType("TIME");
                entity.Property(e => e.RoomType).IsRequired().HasMaxLength(20).HasDefaultValue("Shared");
                entity.Property(e => e.TotalValue).HasColumnType("DECIMAL(10,2)").IsRequired();
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Confirmed");
                // Removido HasDefaultValueSql - a data será definida automaticamente pelo AdjustDateTimeProperties
                // entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // ReservationCommission configuration
            modelBuilder.Entity<ReservationCommission>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Reservation).WithMany().HasForeignKey(e => e.ReservationId).OnDelete(DeleteBehavior.Cascade);
                entity.Property(e => e.TotalCredits).HasColumnType("DECIMAL(10,2)").IsRequired();
                entity.Property(e => e.CommissionPercentage).HasColumnType("DECIMAL(5,2)").IsRequired();
                entity.Property(e => e.CommissionAmount).HasColumnType("DECIMAL(10,2)").IsRequired();
                entity.Property(e => e.CoworkingAmount).HasColumnType("DECIMAL(10,2)").IsRequired();
                // Removido HasDefaultValueSql - a data será definida automaticamente pelo AdjustDateTimeProperties
                // entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // PlatformCreditBalance configuration (singleton: uma linha com o saldo da plataforma)
            modelBuilder.Entity<PlatformCreditBalance>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TotalCredits).HasColumnType("DECIMAL(12,2)").IsRequired();
            });

            // CoworkingRoom configuration
            modelBuilder.Entity<CoworkingRoom>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.CoworkingAddress)
                    .WithMany(a => a.Rooms)
                    .HasForeignKey(e => e.CoworkingAddressId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.Property(e => e.RoomType).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
                // Removido HasDefaultValueSql - a data será definida automaticamente pelo AdjustDateTimeProperties
                // entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // CoworkingRoomPhoto configuration
            modelBuilder.Entity<CoworkingRoomPhoto>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.CoworkingRoom)
                    .WithMany(r => r.Photos)
                    .HasForeignKey(e => e.CoworkingRoomId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
                // Removido HasDefaultValueSql - a data será definida automaticamente pelo AdjustDateTimeProperties
                // entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // CoworkingOperatingHours configuration
            modelBuilder.Entity<CoworkingOperatingHours>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Coworking)
                    .WithMany()
                    .HasForeignKey(e => e.CoworkingId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.CoworkingId, e.DayOfWeek }).IsUnique();
                entity.Property(e => e.DayOfWeek).IsRequired().HasMaxLength(20);
                // Removido HasDefaultValueSql - a data será definida automaticamente pelo AdjustDateTimeProperties
                // entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // BlockedDate configuration
            modelBuilder.Entity<BlockedDate>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Coworking)
                    .WithMany()
                    .HasForeignKey(e => e.CoworkingId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.CoworkingId, e.BlockedDateValue });
                entity.Property(e => e.Reason).HasMaxLength(200);
                // Removido HasDefaultValueSql - a data será definida automaticamente pelo AdjustDateTimeProperties
                // entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // Service configuration
            modelBuilder.Entity<Service>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.Route).IsRequired().HasMaxLength(500);
                entity.Property(e => e.ImageUrl).HasMaxLength(500);
                // Removido HasDefaultValueSql - a data será definida automaticamente pelo AdjustDateTimeProperties
                // entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // PartnerService configuration
            modelBuilder.Entity<PartnerService>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Coworking)
                    .WithMany()
                    .HasForeignKey(e => e.CoworkingId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Service)
                    .WithMany(s => s.PartnerServices)
                    .HasForeignKey(e => e.ServiceId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => new { e.CoworkingId, e.ServiceId }).IsUnique();
                entity.Property(e => e.Percentage).HasColumnType("DECIMAL(5,2)").IsRequired();
                entity.Property(e => e.RemovalRequestStatus).HasConversion<string>().HasMaxLength(50);
                // Removido HasDefaultValueSql - a data será definida automaticamente pelo AdjustDateTimeProperties
                // entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // SefazConfiguration configuration (uma configuração por estado)
            modelBuilder.Entity<SefazConfiguration>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.State).IsRequired().HasMaxLength(2);
                entity.HasIndex(e => new { e.State, e.Environment }).IsUnique();
                entity.Property(e => e.Environment).IsRequired().HasMaxLength(20);
                entity.Property(e => e.PlatformCNPJ).IsRequired().HasMaxLength(18);
                entity.Property(e => e.PlatformMunicipalRegistration).HasMaxLength(50);
                entity.Property(e => e.IntermediationServiceCode).HasMaxLength(20);
                entity.Property(e => e.IntermediationServiceDescription).HasMaxLength(500);
                entity.Property(e => e.PlatformCNAE).HasMaxLength(20);
                entity.Property(e => e.TaxRegime).HasMaxLength(100);
                entity.Property(e => e.ISSMunicipality).HasMaxLength(200);
                entity.Property(e => e.ISSAliquot).HasColumnType("DECIMAL(5,2)");
                // Removido HasDefaultValueSql - a data será definida automaticamente pelo AdjustDateTimeProperties
                // entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // CoworkingSefazConfiguration configuration (configuração SEFAZ por parceiro e estado)
            modelBuilder.Entity<CoworkingSefazConfiguration>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Coworking)
                    .WithMany()
                    .HasForeignKey(e => e.CoworkingId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.Property(e => e.State).IsRequired().HasMaxLength(2);
                entity.HasIndex(e => new { e.CoworkingId, e.State, e.Environment }).IsUnique();
                entity.Property(e => e.Environment).IsRequired().HasMaxLength(20);
                entity.Property(e => e.PartnerCNPJ).IsRequired().HasMaxLength(18);
                entity.Property(e => e.PartnerMunicipalRegistration).HasMaxLength(50);
                entity.Property(e => e.ServiceCode).HasMaxLength(20);
                entity.Property(e => e.ServiceDescription).HasMaxLength(500);
                entity.Property(e => e.PartnerCNAE).HasMaxLength(20);
                entity.Property(e => e.TaxRegime).HasMaxLength(100);
                entity.Property(e => e.ISSMunicipality).HasMaxLength(200);
                entity.Property(e => e.ISSAliquot).HasColumnType("DECIMAL(5,2)");
                // Removido HasDefaultValueSql - a data será definida automaticamente pelo AdjustDateTimeProperties
                // entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // FiscalNote configuration
            modelBuilder.Entity<FiscalNote>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Reservation)
                    .WithMany()
                    .HasForeignKey(e => e.ReservationId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(e => e.Contract)
                    .WithMany()
                    .HasForeignKey(e => e.ContractId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(e => e.Payment)
                    .WithMany()
                    .HasForeignKey(e => e.PaymentId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(e => e.Coworking)
                    .WithMany()
                    .HasForeignKey(e => e.CoworkingId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(e => e.Client)
                    .WithMany()
                    .HasForeignKey(e => e.ClientId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.Property(e => e.Type).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Number).HasMaxLength(50);
                entity.Property(e => e.VerificationCode).HasMaxLength(50);
                entity.Property(e => e.AccessKey).HasMaxLength(50);
                entity.Property(e => e.Amount).HasColumnType("DECIMAL(10,2)").IsRequired();
                entity.Property(e => e.CommissionAmount).HasColumnType("DECIMAL(10,2)");
                entity.Property(e => e.ServiceAmount).HasColumnType("DECIMAL(10,2)");
                entity.Property(e => e.IdempotencyKey).HasMaxLength(64);
                entity.HasIndex(e => e.AccessKey);
                entity.HasIndex(e => new { e.Type, e.ReservationId, e.ContractId });
                entity.HasIndex(e => e.IdempotencyKey).IsUnique();
                // Removido HasDefaultValueSql - a data será definida automaticamente pelo AdjustDateTimeProperties
                // entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // FiscalNoteScriptExecution configuration
            modelBuilder.Entity<FiscalNoteScriptExecution>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ExecutedByUserName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.NoteType).IsRequired().HasMaxLength(20);
                entity.Property(e => e.ErrorDetails).HasMaxLength(4000);
                entity.HasOne(e => e.ExecutedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.ExecutedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(e => e.ExecutedAt);
            });

            // PaymentHistory configuration
            modelBuilder.Entity<PaymentHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Payment)
                    .WithMany()
                    .HasForeignKey(e => e.PaymentId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.ActionByUser)
                    .WithMany()
                    .HasForeignKey(e => e.ActionBy)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
                entity.Property(e => e.RejectionReason).HasMaxLength(1000);
                // Remover valor padrão do banco - vamos definir explicitamente no código
                // entity.Property(e => e.ActionAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.HasIndex(e => e.PaymentId);
            });

            // CapacityChangeRequest configuration
            modelBuilder.Entity<CapacityChangeRequest>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.CoworkingAddress)
                    .WithMany(a => a.CapacityChangeRequests)
                    .HasForeignKey(e => e.CoworkingAddressId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Reviewer)
                    .WithMany()
                    .HasForeignKey(e => e.ReviewedBy)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
                entity.Property(e => e.Justification).HasMaxLength(2000);
                entity.Property(e => e.RejectionReason).HasMaxLength(2000);
                entity.HasIndex(e => e.CoworkingAddressId);
                entity.HasIndex(e => e.Status);
            });
        }
    }
}
