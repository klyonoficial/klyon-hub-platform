# KlyonHubPlatform.Domain.Services

Esta pasta contém serviços de domínio que implementam regras de negócio complexas que não pertencem a uma entidade específica.

## Estrutura

Os serviços de domínio devem:
- Implementar regras de negócio que envolvem múltiplas entidades
- Não depender de infraestrutura (repositórios, banco de dados, etc.)
- Ser testáveis de forma isolada
- Operar apenas com entidades de domínio

## Exemplos de serviços de domínio

- Validação de regras de negócio complexas
- Cálculos de comissões
- Geração de números de contrato
- Validação de elegibilidade para contratos
- Regras de suspensão/cancelamento
