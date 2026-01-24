# Por que um endereço não aparece em “Escolher Localidade” (Client/ChooseLocation)?

## Resumo

**Meus Endereços** (Parceiro/MyAddresses) e **Escolher Localidade** (Client/ChooseLocation) usam **regras diferentes**. Um endereço pode aparecer como “Disponível” para o parceiro e **não** aparecer para o cliente.

---

## Onde cada lista é montada

| Tela | Controller | Origem dos dados |
|------|------------|-------------------|
| **Meus Endereços** (Parceiro) | `CoworkingController.MyAddresses` | Todos os endereços do **coworking logado**, filtrados por busca/status/localidade |
| **Escolher Localidade** (Cliente) | `ClientController.ChooseLocation` | `CoworkingRepository.GetAvailableAddressesAsync()` + localidades ativas |

---

## Critérios em “Meus Endereços” (Parceiro)

- Endereços do **mesmo Coworking** do usuário logado.
- Filtros opcionais: busca, status (Disponível/Indisponível = `IsAvailable`), localidade.
- **“Disponível”** na tabela = apenas `address.IsAvailable == true`. Nada mais é verificado.

Ou seja: o parceiro vê o endereço e o status “Disponível” com base só em `IsAvailable`.

---

## Critérios em “Escolher Localidade” (Cliente)

A lista de localidades é montada assim:

1. Buscar **localidades ativas**: `Locations` com `IsActive = true`.
2. Buscar **endereços disponíveis** via `GetAvailableAddressesAsync()` (veja condições abaixo).
3. Manter apenas endereços cuja **localidade** está entre as ativas.
4. Contar quantos endereços há **por localidade**.
5. Mostrar só **localidades com pelo menos 1 endereço** disponível (ordenadas por disponibilidade).

Se **nenhum** endereço passar em `GetAvailableAddressesAsync`, o resultado é **“0 de 0 localidades”** e a mensagem *“Nenhuma localidade com endereço disponível no momento”*.

---

## Condições de `GetAvailableAddressesAsync` (repositório)

Para um endereço entrar na lista do cliente, **todas** as condições abaixo precisam ser verdadeiras.

### 1. Serviço “Contratar Endereço Fiscal”

- Existe um `Service` com `Name = 'Contratar Endereço Fiscal'` e `IsActive = true`.
- Caso contrário: **lista vazia** para todos.

### 2. PartnerServices (serviço do parceiro)

- O **coworking** do endereço precisa ter em `PartnerServices` o serviço “Contratar Endereço Fiscal” com **`Percentage > 0`**.
- Se o coworking não tiver esse serviço ou tiver percentagem 0: **seus endereços não entram** na lista.

### 3. Endereço (CoworkingAddress)

| Campo | Exigência |
|-------|-----------|
| `IsAvailable` | `true` |
| `AddressStatus` | `Active` (0) — não Suspended nem Removed |
| `CurrentContracts` | &lt; `MaxContracts` |

### 4. Coworking

| Campo | Exigência |
|-------|-----------|
| `IsActive` | `true` |
| `IsPartner` | `true` |
| `ApprovalStatus` | `Approved` (2) — não UnderReview nem AwaitingAdjustment |

### 5. Localidade (Location)

- A `Location` do endereço precisa estar entre as **ativas** (`IsActive = true`), pois o `ChooseLocation` filtra por `Locations` ativas.

---

## Checklist rápido (por que não aparece?)

Use o script SQL `choose_location_diagnostico.sql` na raiz do repositório: ajuste `@endereco_busca` (ex.: `'%Rei Carlos V%'`) e execute no MySQL para checar o coworking/endereço desejado. Resumo do que conferir:

1. **Serviço** “Contratar Endereço Fiscal” existe e está ativo?
2. **PartnerServices**: o coworking tem esse serviço com **percentagem > 0**?
3. **Endereço**: `IsAvailable = 1`, `AddressStatus = 0` (Active), `CurrentContracts < MaxContracts`?
4. **Coworking**: `IsActive = 1`, `IsPartner = 1`, `ApprovalStatus = 'Approved'`?
5. **Location**: `IsActive = 1`?

O motivo mais comum de o endereço **não** aparecer em ChooseLocation:

- **Coworking não aprovado** (`ApprovalStatus` ≠ `Approved`), ou  
- **Serviço “Contratar Endereço Fiscal”** sem percentagem configurada (**PartnerServices** sem registro ou `Percentage = 0`).

---

## Referência de código

- **ChooseLocation**: `ClientController.ChooseLocation` → `_coworkingService.GetAvailableAddressesAsync()`.
- **GetAvailableAddressesAsync**: `CoworkingRepository.GetAvailableAddressesAsync` (validações acima).
- **MyAddresses**: `CoworkingController.MyAddresses` — apenas endereços do coworking, “Disponível” = `IsAvailable`.
