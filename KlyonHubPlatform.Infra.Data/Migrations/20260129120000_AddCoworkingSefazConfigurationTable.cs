using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoworkingSefazConfigurationTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `CoworkingSefazConfigurations` (
                    `Id` INT NOT NULL AUTO_INCREMENT,
                    `CoworkingId` INT NOT NULL,
                    `State` VARCHAR(2) CHARACTER SET utf8mb4 NOT NULL,
                    `Environment` VARCHAR(20) CHARACTER SET utf8mb4 NOT NULL,
                    `PartnerCNPJ` VARCHAR(18) CHARACTER SET utf8mb4 NOT NULL,
                    `PartnerMunicipalRegistration` VARCHAR(50) CHARACTER SET utf8mb4 NOT NULL,
                    `CertificatePath` LONGTEXT CHARACTER SET utf8mb4 NULL,
                    `CertificatePassword` LONGTEXT CHARACTER SET utf8mb4 NULL,
                    `CertificateContent` LONGTEXT CHARACTER SET utf8mb4 NULL,
                    `ServiceCode` VARCHAR(20) CHARACTER SET utf8mb4 NOT NULL,
                    `ServiceDescription` VARCHAR(500) CHARACTER SET utf8mb4 NOT NULL,
                    `PartnerCNAE` VARCHAR(20) CHARACTER SET utf8mb4 NOT NULL,
                    `TaxRegime` VARCHAR(100) CHARACTER SET utf8mb4 NOT NULL,
                    `ISSAliquot` DECIMAL(5,2) NOT NULL,
                    `ISSMunicipality` VARCHAR(200) CHARACTER SET utf8mb4 NOT NULL,
                    `HomologationUrl` LONGTEXT CHARACTER SET utf8mb4 NULL,
                    `ProductionUrl` LONGTEXT CHARACTER SET utf8mb4 NULL,
                    `ApiToken` LONGTEXT CHARACTER SET utf8mb4 NULL,
                    `ApiKey` LONGTEXT CHARACTER SET utf8mb4 NULL,
                    `TimeoutSeconds` INT NOT NULL,
                    `IsActive` TINYINT(1) NOT NULL,
                    `CreatedAt` DATETIME(6) NOT NULL,
                    `UpdatedAt` DATETIME(6) NULL,
                    `Notes` LONGTEXT CHARACTER SET utf8mb4 NULL,
                    PRIMARY KEY (`Id`),
                    UNIQUE KEY `IX_CoworkingSefazConfigurations_CoworkingId_State_Environment` (`CoworkingId`, `State`, `Environment`),
                    CONSTRAINT `FK_CoworkingSefazConfigurations_Coworkings_CoworkingId`
                        FOREIGN KEY (`CoworkingId`) REFERENCES `Coworkings` (`Id`) ON DELETE CASCADE
                ) CHARACTER SET utf8mb4;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS `CoworkingSefazConfigurations`;");
        }
    }
}
