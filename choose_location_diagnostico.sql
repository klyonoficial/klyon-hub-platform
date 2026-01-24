-- =============================================================================
-- Diagnóstico: por que um endereço NÃO aparece em Client/ChooseLocation?
-- Execute no MySQL. Ajuste o endereço na variável @endereco_busca se quiser
-- filtrar por um endereço específico (ex: 'Rei Carlos V').
-- =============================================================================

SET @endereco_busca = '%Rei Carlos V%';  -- use '%' para listar todos

-- -----------------------------------------------------------------------------
-- 1. Serviço "Contratar Endereço Fiscal"
-- -----------------------------------------------------------------------------
SELECT '1. Serviço Contratar Endereço Fiscal' AS Etapa;
SELECT Id, Name, IsActive,
       CASE WHEN Name = 'Contratar Endereço Fiscal' AND IsActive THEN 'OK' ELSE 'FALHA' END AS Status
FROM Services
WHERE Name = 'Contratar Endereço Fiscal';
-- Se vazio: criar/ativar o serviço. GetAvailableAddresses retorna lista vazia.

-- -----------------------------------------------------------------------------
-- 2. Endereços do parceiro + Coworking + Location (visão geral)
-- -----------------------------------------------------------------------------
SELECT '2. Endereços cadastrados (parceiro)' AS Etapa;
SELECT
    ca.Id AS AddressId,
    ca.Address,
    ca.Number,
    ca.City,
    ca.State,
    ca.IsAvailable,
    ca.AddressStatus,  -- 0=Active, 1=Suspended, 2=Removed
    ca.CurrentContracts,
    ca.MaxContracts,
    c.Id AS CoworkingId,
    c.Name AS CoworkingName,
    c.IsActive AS CoworkingIsActive,
    c.IsPartner,
    c.ApprovalStatus,  -- 'Approved' necessário para ChooseLocation
    l.Id AS LocationId,
    CONCAT(l.City, '/', l.State) AS Localidade,
    l.IsActive AS LocationIsActive
FROM CoworkingAddresses ca
JOIN Coworkings c ON c.Id = ca.CoworkingId
JOIN Locations l ON l.Id = ca.LocationId
WHERE ca.Address LIKE @endereco_busca OR @endereco_busca = '%';

-- -----------------------------------------------------------------------------
-- 3. PartnerServices: "Contratar Endereço Fiscal" com Percentage > 0
-- -----------------------------------------------------------------------------
SELECT '3. PartnerServices (Contratar Endereço Fiscal, Percentage > 0)' AS Etapa;
SELECT
    ps.Id,
    ps.CoworkingId,
    c.Name AS CoworkingName,
    s.Name AS ServiceName,
    ps.Percentage,
    CASE WHEN ps.Percentage > 0 THEN 'OK' ELSE 'FALHA (percentagem precisa ser > 0)' END AS Status
FROM PartnerServices ps
JOIN Services s ON s.Id = ps.ServiceId
JOIN Coworkings c ON c.Id = ps.CoworkingId
WHERE s.Name = 'Contratar Endereço Fiscal'
  AND (c.Id IN (SELECT CoworkingId FROM CoworkingAddresses WHERE Address LIKE @endereco_busca) OR @endereco_busca = '%');
-- Se vazio ou Percentage = 0: endereços desse coworking NÃO entram em ChooseLocation.

-- -----------------------------------------------------------------------------
-- 4. Verificação por endereço: todas as condições do GetAvailableAddresses
-- -----------------------------------------------------------------------------
SELECT '4. Checagem por endereço (simula GetAvailableAddresses)' AS Etapa;
SELECT
    ca.Id AS AddressId,
    ca.Address,
    CONCAT(ca.City, '/', ca.State) AS CidadeEstado,
    CONCAT(l.City, '/', l.State) AS Localidade,
    ca.IsAvailable,
    ca.AddressStatus,
    ca.CurrentContracts,
    ca.MaxContracts,
    (ca.CurrentContracts < ca.MaxContracts) AS TemVaga,
    c.IsActive AS CoworkingIsActive,
    c.IsPartner,
    c.ApprovalStatus,
    (c.ApprovalStatus = 'Approved') AS CoworkingAprovado,
    l.IsActive AS LocationIsActive,
    (SELECT COUNT(*) FROM PartnerServices ps2
     JOIN Services s2 ON s2.Id = ps2.ServiceId
     WHERE ps2.CoworkingId = c.Id AND s2.Name = 'Contratar Endereço Fiscal' AND s2.IsActive AND ps2.Percentage > 0) AS TemServicoPercentagem,
    CASE
        WHEN (SELECT 1 FROM Services s WHERE s.Name = 'Contratar Endereço Fiscal' AND s.IsActive) IS NULL THEN 'Serviço inexistente/inativo'
        WHEN NOT (ca.IsAvailable AND ca.AddressStatus = 0 AND ca.CurrentContracts < ca.MaxContracts) THEN 'Endereço: IsAvailable/Active/vaga'
        WHEN NOT (c.IsActive AND c.IsPartner AND c.ApprovalStatus = 'Approved') THEN 'Coworking: ativo, parceiro, aprovado'
        WHEN NOT l.IsActive THEN 'Localidade inativa'
        WHEN (SELECT COUNT(*) FROM PartnerServices ps2 JOIN Services s2 ON s2.Id = ps2.ServiceId
              WHERE ps2.CoworkingId = c.Id AND s2.Name = 'Contratar Endereço Fiscal' AND s2.IsActive AND ps2.Percentage > 0) = 0
            THEN 'Sem PartnerService Contratar Endereço Fiscal com % > 0'
        ELSE 'OK – deveria aparecer em ChooseLocation'
    END AS Diagnostico
FROM CoworkingAddresses ca
JOIN Coworkings c ON c.Id = ca.CoworkingId
JOIN Locations l ON l.Id = ca.LocationId
WHERE ca.Address LIKE @endereco_busca OR @endereco_busca = '%';

-- -----------------------------------------------------------------------------
-- 5. Resumo: o que falta para aparecer?
-- -----------------------------------------------------------------------------
SELECT '5. Resumo' AS Etapa;
SELECT
    CASE WHEN EXISTS (SELECT 1 FROM Services WHERE Name = 'Contratar Endereço Fiscal' AND IsActive) THEN 'OK' ELSE 'FALTA: serviço ativo' END AS Servico,
    CASE WHEN EXISTS (
        SELECT 1 FROM CoworkingAddresses ca
        JOIN Coworkings c ON c.Id = ca.CoworkingId
        JOIN PartnerServices ps ON ps.CoworkingId = c.Id
        JOIN Services s ON s.Id = ps.ServiceId
        WHERE s.Name = 'Contratar Endereço Fiscal' AND s.IsActive AND ps.Percentage > 0
          AND (ca.Address LIKE @endereco_busca OR @endereco_busca = '%')
    ) THEN 'OK' ELSE 'FALTA: PartnerService % > 0' END AS PartnerService,
    CASE WHEN EXISTS (
        SELECT 1 FROM CoworkingAddresses ca
        JOIN Coworkings c ON c.Id = ca.CoworkingId
        WHERE c.IsActive AND c.IsPartner AND c.ApprovalStatus = 'Approved'
          AND (ca.Address LIKE @endereco_busca OR @endereco_busca = '%')
    ) THEN 'OK' ELSE 'FALTA: Coworking ativo/parceiro/aprovado' END AS Coworking,
    CASE WHEN EXISTS (
        SELECT 1 FROM CoworkingAddresses ca
        JOIN Locations l ON l.Id = ca.LocationId
        WHERE ca.IsAvailable AND ca.AddressStatus = 0 AND ca.CurrentContracts < ca.MaxContracts AND l.IsActive
          AND (ca.Address LIKE @endereco_busca OR @endereco_busca = '%')
    ) THEN 'OK' ELSE 'FALTA: endereço disponível ou localidade ativa' END AS EnderecoLocalidade;
