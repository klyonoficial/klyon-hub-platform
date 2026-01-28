-- Adicionar colunas para MaxContractsChangeRequest na tabela CoworkingAddresses
ALTER TABLE `CoworkingAddresses`
ADD COLUMN `RequestedMaxContracts` INT NOT NULL DEFAULT 1 AFTER `MaxContracts`,
ADD COLUMN `MaxContractsApprovalStatus` VARCHAR(50) NOT NULL DEFAULT 'None' AFTER `RequestedMaxContracts`;

-- Criar tabela MaxContractsChangeRequests
CREATE TABLE IF NOT EXISTS `MaxContractsChangeRequests` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `CoworkingAddressId` INT NOT NULL,
    `RequestedMaxContracts` INT NOT NULL,
    `PreviousMaxContracts` INT NOT NULL,
    `Status` VARCHAR(50) NOT NULL DEFAULT 'Pending',
    `Justification` VARCHAR(2000) NULL,
    `RejectionReason` VARCHAR(2000) NULL,
    `ReviewedBy` INT NULL,
    `ReviewedAt` DATETIME(6) NULL,
    `CreatedAt` DATETIME(6) NOT NULL,
    `UpdatedAt` DATETIME(6) NULL,
    PRIMARY KEY (`Id`),
    INDEX `IX_MaxContractsChangeRequests_CoworkingAddressId` (`CoworkingAddressId`),
    INDEX `IX_MaxContractsChangeRequests_Status` (`Status`),
    CONSTRAINT `FK_MaxContractsChangeRequests_CoworkingAddresses_CoworkingAddressId` 
        FOREIGN KEY (`CoworkingAddressId`) 
        REFERENCES `CoworkingAddresses` (`Id`) 
        ON DELETE CASCADE,
    CONSTRAINT `FK_MaxContractsChangeRequests_Users_ReviewedBy` 
        FOREIGN KEY (`ReviewedBy`) 
        REFERENCES `Users` (`Id`) 
        ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
