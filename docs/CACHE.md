# Configuração de Cache de Navegador

## Visão Geral

O sistema implementa controle de cache de navegador via headers HTTP no backend, garantindo que dados sensíveis não sejam armazenados em cache e que dados públicos possam ser otimizados com cache.

## Como Funciona

O middleware `CacheControlMiddleware` analisa cada resposta HTTP e define os headers apropriados baseado no tipo de rota:

### Dados Sensíveis (no-store)

**Headers aplicados:**
```
Cache-Control: no-store, no-cache, must-revalidate, private
Pragma: no-cache
Expires: 0
```

**Rotas afetadas:**
- `/Admin/*` - Todas as rotas administrativas
- Rotas financeiras: `/payment`, `/payout`, `/commission`, `/credit`, `/pagbank`, `/webhook`
- Rotas contratuais: `/contract`, `/subscription`
- Rotas de autenticação: `/auth/*`, `/login`, `/logout`
- Dados pessoais de clientes: 
  - `/client/mycontracts`, `/client/contractdetails`, `/client/profile`
  - `/client/history` - Histórico unificado do cliente
  - `/client/notifications` - Central de notificações do cliente
  - `/client/documents` - Download centralizado de documentos
- Dados sensíveis de coworkings/parceiros: 
  - `/coworking/activecontracts`, `/coworking/commissions`, `/coworking/contractdetails`, `/coworking/mycontract`, `/coworking/profile`, `/coworking/sefaz`
  - `/partner/financial/history` ou `/coworking/financial/history` - Histórico financeiro detalhado do parceiro
  - `/partner/notifications` ou `/coworking/notifications` - Central de notificações do parceiro
  - `/partner/addresses/{id}/capacityrequest` - Solicitação de aumento de capacidade
- Documentos e auditoria: `/document`, `/audit`, `/fiscalnote`
- Relatórios: `/report`

### Dados Públicos (cache de 24 horas)

**Headers aplicados:**
```
Cache-Control: public, max-age=86400
```

**Rotas afetadas:**
- `/` - Página inicial
- `/Home/*` - Páginas públicas
- `/public/*` - Conteúdo público
- Listagens públicas de coworkings (sem dados pessoais)

### Arquivos Estáticos (cache de 1 ano)

**Headers aplicados:**
```
Cache-Control: public, max-age=31536000
```

**Arquivos afetados:**
- `.css`, `.js`, `.jpg`, `.jpeg`, `.png`, `.gif`, `.svg`, `.ico`
- `.woff`, `.woff2`, `.ttf`, `.eot` (fontes)

### Rotas Padrão (cache de 1 hora)

**Headers aplicados:**
```
Cache-Control: public, max-age=3600
```

**Aplicado a:** Rotas que não se enquadram nas categorias acima.

## Regras para a Equipe

1. **Cache é configurado via headers HTTP no backend** - Não é necessária instalação adicional no servidor.

2. **Dados públicos podem e devem usar cache** - Melhora a performance e reduz carga no servidor.

3. **Dados sensíveis OBRIGATORIAMENTE usam `no-store`** - Dados financeiros, administrativos ou contratuais nunca devem ser armazenados em cache pelo navegador.

4. **O middleware é aplicado automaticamente** - Não é necessário configurar headers manualmente nos controllers.

## Personalização

Para adicionar novas rotas sensíveis ou públicas, edite o arquivo:
```
KlyonHubPlatform.Infra.CrossCutting/Middleware/CacheControlMiddleware.cs
```

Métodos para modificar:
- `IsSensitiveRoute(string path)` - Adicione padrões de rotas sensíveis
- `IsPublicRoute(string path)` - Adicione padrões de rotas públicas
- `IsStaticAsset(string path)` - Adicione extensões de arquivos estáticos

## Exemplo de Uso

O middleware é registrado automaticamente no `Program.cs`:

```csharp
app.UseStaticFiles();
app.UseMiddleware<CacheControlMiddleware>();
```

Não é necessário fazer nada adicional nos controllers - o cache é aplicado automaticamente baseado na rota.

## Verificação

Para verificar se os headers estão sendo aplicados corretamente:

1. Abra o DevTools do navegador (F12)
2. Vá para a aba "Network"
3. Recarregue a página
4. Clique em uma requisição
5. Verifique os headers de resposta, especialmente `Cache-Control`

## Notas Importantes

- O middleware só aplica headers em respostas bem-sucedas (status 200-299)
- Headers de cache são aplicados após o processamento da requisição
- Arquivos estáticos já são gerenciados pelo `UseStaticFiles()`, mas o middleware garante headers consistentes
