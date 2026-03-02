# Diagrama de Classes (Baseado nas US)

Este diagrama foi derivado das User Stories em `docs/us/` e representa uma visão de domínio de alto nível do **Klyon Hub Platform**.

## Diagrama Draw.io

Arquivo fonte: [diagrama-classes-us.drawio](./diagrama-classes-us.drawio). Pode ser editado no [draw.io](https://app.diagrams.net/) ou no VS Code com extensão Draw.io.

## Visão em Mermaid

```mermaid
classDiagram

class Role {
  +id: int
  +name: string
  +description: string
  +createdAt: datetime
}

class User {
  +id: int
  +email: string
  +fullName: string
  +isActive: bool
  +emailVerified: bool
  +createdAt: datetime
}

class UserRole {
  +userId: int
  +roleId: int
  +assignedAt: datetime
}

class Location {
  +id: int
  +city: string
  +state: string
  +region: string?
  +isActive: bool
}

class Client {
  +id: int
  +userId: int
  +razaoSocial: string
  +nomeFantasia: string
  +cnpj: string
  +cpf: string
  +isActive: bool
  +createdAt: datetime
}

class Coworking {
  +id: int
  +userId: int
  +name: string
  +cnpj: string
  +isActive: bool
  +approvalStatus: CoworkingApprovalStatus
  +createdAt: datetime
}

class CoworkingAddress {
  +id: int
  +coworkingId: int
  +locationId: int
  +address: string
  +basePrice: decimal
  +maxContracts: int
  +addressStatus: AddressStatus
}

class Contract {
  +id: int
  +clientId: int
  +coworkingAddressId: int
  +contractNumber: string
  +startDate: date
  +endDate: date?
  +status: ContractStatus
  +monthlyValue: decimal
  +createdAt: datetime
}

class Subscription {
  +id: int
  +contractId: int
  +externalSubscriptionId: string
  +status: SubscriptionStatus
  +monthlyAmount: decimal
  +nextBillingDate: date
  +createdAt: datetime
}

class Payment {
  +id: int
  +subscriptionId: int?
  +reservationId: int?
  +amount: decimal
  +status: PaymentStatus
  +method: PaymentMethod
  +dueDate: date
  +paidAt: datetime?
  +createdAt: datetime
}

class Reservation {
  +id: int
  +clientId: int
  +coworkingAddressId: int
  +reservationDate: date
  +totalValue: decimal
  +status: ReservationStatus
  +createdAt: datetime
}

class CoworkingProduct {
  +id: int
  +coworkingId: int
  +productType: string
  +isActive: bool
}

class ContractStatus {
  <<enumeration>>
  Active
  Suspended
  PendingPayment
  Cancelled
  Expired
  PendingCancellation
}

class SubscriptionStatus {
  <<enumeration>>
  Active
  Suspended
  Cancelled
  PastDue
}

class PaymentStatus {
  <<enumeration>>
  Pending
  Processing
  AwaitingValidation
  Paid
}

class PaymentMethod {
  <<enumeration>>
  CreditCard
  PIX
  BankTransfer
}

class CoworkingApprovalStatus {
  <<enumeration>>
  UnderReview
  AwaitingAdjustment
  Approved
}

class ReservationStatus {
  <<enumeration>>
  Pending
  Confirmed
  Cancelled
  Completed
}

User "1" --> "*" UserRole : possui
UserRole "*" --> "1" Role : referencia
Client "*" --> "1" User : vinculado a
Coworking "*" --> "1" User : vinculado a
CoworkingAddress "*" --> "1" Coworking : pertence a
CoworkingAddress "*" --> "1" Location : localização
Contract "*" --> "1" Client : cliente
Contract "*" --> "1" CoworkingAddress : endereço
Subscription "*" --> "1" Contract : contrato
Payment "*" --> "0..1" Subscription : assinatura
Payment "*" --> "0..1" Reservation : reserva
Reservation "*" --> "1" Client : cliente
Reservation "*" --> "1" CoworkingAddress : endereço
CoworkingProduct "*" --> "1" Coworking : produto de

Coworking --> CoworkingApprovalStatus : status
Contract --> ContractStatus : status
Subscription --> SubscriptionStatus : status
Payment --> PaymentStatus : status
Payment --> PaymentMethod : método
Reservation --> ReservationStatus : status
```

## Observações

- O modelo representa classes de domínio sugeridas pelas US e pelo código em `KlyonHubPlatform.Domain` e não substitui o modelo técnico final.
- Campos e relacionamentos podem ser refinados conforme regras de negócio detalhadas e decisões de implementação.
- A aplicação segue arquitetura em camadas (Api, Application, Domain, Infra.Data, Infra.CrossCutting); as entidades acima pertencem ao **Domain**.
