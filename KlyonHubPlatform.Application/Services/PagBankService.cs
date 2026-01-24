using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.DTOs.PagBank;
using KlyonHubPlatform.Domain.Core.Enums;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Linq;

namespace KlyonHubPlatform.Application.Services
{
    public class PagBankService : IPagBankService
    {
        private readonly ISystemConfigurationService _systemConfigService;
        private readonly ILogger<PagBankService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _sandboxBaseUrl = "https://sandbox.api.pagseguro.com";
        private readonly string _productionBaseUrl = "https://api.pagseguro.com";

        public PagBankService(
            ISystemConfigurationService systemConfigService,
            ILogger<PagBankService> logger,
            HttpClient httpClient)
        {
            _systemConfigService = systemConfigService;
            _logger = logger;
            _httpClient = httpClient;
        }

        private async Task<string> GetBaseUrlAsync()
        {
            var useSandbox = await _systemConfigService.GetValueAsync("PagBank.UseSandbox");
            var isSandbox = useSandbox?.ToLower() == "true";
            return isSandbox ? _sandboxBaseUrl : _productionBaseUrl;
        }

        private async Task<string> GetAccessTokenAsync()
        {
            var token = await _systemConfigService.GetValueAsync("PagBank.AccessToken");
            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("Token de acesso PagBank não configurado. Configure em Configurações do Sistema > PagBank.AccessToken");
            return token;
        }

        private async Task<HttpRequestMessage> CreateAuthenticatedRequestAsync(HttpMethod method, string endpoint)
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = await GetBaseUrlAsync();
            var request = new HttpRequestMessage(method, $"{baseUrl}{endpoint}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return request;
        }

        public async Task<OrderResponse> CreateOrderAsync(string referenceId, decimal amount, PaymentMethod method,
            string clientName, string clientEmail, string clientTaxId, string? clientPhone = null,
            PagBankCard? card = null, int? installments = null, PagBankAddress? clientAddress = null)
        {
            try
            {
                var orderRequest = new OrderRequest
                {
                    ReferenceId = referenceId,
                    Customer = new PagBankCustomer
                    {
                        Name = clientName,
                        Email = clientEmail,
                        TaxId = clientTaxId.Replace(".", "").Replace("/", "").Replace("-", ""),
                        Phone = !string.IsNullOrEmpty(clientPhone) ? ParsePhone(clientPhone) : null
                    },
                    Items = new List<PagBankItem>
                    {
                        new PagBankItem
                        {
                            ReferenceId = referenceId,
                            Name = "Endereço Fiscal",
                            Quantity = 1,
                            UnitAmount = (int)(amount * 100) // Converter para centavos
                        }
                    }
                };

                // Configurar método de pagamento
                if (method == PaymentMethod.PIX)
                {
                    orderRequest.QrCodes = new List<PagBankQrCode>
                    {
                        new PagBankQrCode
                        {
                            Amount = new PagBankAmount
                            {
                                Value = (int)(amount * 100),
                                Currency = "BRL"
                            }
                        }
                    };
                }
                else if (method == PaymentMethod.BankTransfer)
                {
                    // Função auxiliar para validar e preencher campos obrigatórios
                    string GetValueOrDefault(string? value, string defaultValue = "Não informado")
                    {
                        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
                    }
                    
                    // Validar e preparar dados do titular do boleto (todos obrigatórios)
                    var holderName = GetValueOrDefault(clientName, "Não informado");
                    if (string.IsNullOrWhiteSpace(holderName) || holderName == "Não informado")
                    {
                        holderName = "Cliente";
                    }
                    
                    // TaxId (CPF/CNPJ) - obrigatório, remover formatação
                    var holderTaxId = !string.IsNullOrWhiteSpace(clientTaxId)
                        ? clientTaxId.Replace(".", "").Replace("/", "").Replace("-", "").Trim()
                        : "00000000000"; // CPF padrão se não informado
                    
                    // Garantir que TaxId tenha pelo menos 11 dígitos (CPF mínimo)
                    if (holderTaxId.Length < 11)
                    {
                        holderTaxId = holderTaxId.PadLeft(11, '0');
                    }
                    
                    // Email - obrigatório, validar formato básico
                    var holderEmail = GetValueOrDefault(clientEmail, "noreply@klyon.com.br");
                    if (!holderEmail.Contains("@") || holderEmail.Length < 5)
                    {
                        holderEmail = "noreply@klyon.com.br";
                    }
                    
                    // Preparar endereço do holder (todos os campos são obrigatórios)
                    // O holder.address usa "region" ao invés de "region_code"
                    var holderStreet = GetValueOrDefault(clientAddress?.Street, "Não informado");
                    var holderNumber = GetValueOrDefault(clientAddress?.Number, "S/N");
                    var holderComplement = clientAddress?.Complement; // Opcional
                    var holderLocality = GetValueOrDefault(clientAddress?.Locality, "Não informado");
                    var holderCity = GetValueOrDefault(clientAddress?.City, "Não informado");
                    
                    // Region (UF) - obrigatório, deve ser 2 caracteres maiúsculas
                    var holderRegion = "SP"; // Padrão
                    if (!string.IsNullOrWhiteSpace(clientAddress?.RegionCode))
                    {
                        var regionTemp = clientAddress.RegionCode.Trim().ToUpper();
                        holderRegion = regionTemp.Length >= 2 ? regionTemp.Substring(0, 2) : "SP";
                    }
                    
                    // Validar se é uma UF válida do Brasil (lista básica)
                    var validUFs = new[] { "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", 
                                          "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI", "RJ", "RN", 
                                          "RS", "RO", "RR", "SC", "SP", "SE", "TO" };
                    if (!validUFs.Contains(holderRegion))
                    {
                        holderRegion = "SP";
                    }
                    
                    // Country - obrigatório, sempre "BRA" para Brasil
                    var holderCountry = GetValueOrDefault(clientAddress?.Country, "BRA");
                    if (holderCountry != "BRA")
                    {
                        holderCountry = "BRA";
                    }
                    
                    // PostalCode (CEP) - obrigatório, deve ter 8 dígitos
                    var holderPostalCode = "00000000"; // Padrão
                    if (!string.IsNullOrWhiteSpace(clientAddress?.PostalCode))
                    {
                        var postalCodeTemp = clientAddress.PostalCode
                            .Replace("-", "")
                            .Replace(".", "")
                            .Replace(" ", "")
                            .Trim();
                        
                        if (postalCodeTemp.Length == 8 && postalCodeTemp.All(char.IsDigit))
                        {
                            holderPostalCode = postalCodeTemp;
                        }
                        else if (postalCodeTemp.Length > 8)
                        {
                            holderPostalCode = postalCodeTemp.Substring(0, 8);
                        }
                        else
                        {
                            holderPostalCode = postalCodeTemp.PadLeft(8, '0');
                        }
                    }
                    
                    // Criar endereço do holder com todos os campos validados
                    var holderAddress = new PagBankBoletoHolderAddress
                    {
                        Street = holderStreet,
                        Number = holderNumber,
                        Complement = holderComplement,
                        Locality = holderLocality,
                        City = holderCity,
                        Region = holderRegion,
                        RegionCode = holderRegion,
                        Country = holderCountry,
                        PostalCode = holderPostalCode
                    };
                    
                    orderRequest.Charges = new List<PagBankCharge>
                    {
                        new PagBankCharge
                        {
                            ReferenceId = referenceId,
                            Description = "Pagamento de Endereço Fiscal",
                            Amount = new PagBankAmount
                            {
                                Value = (int)(amount * 100),
                                Currency = "BRL"
                            },
                            PaymentMethod = new PagBankPaymentMethod
                            {
                                Type = "BOLETO",
                                Boleto = new PagBankBoleto
                                {
                                    DueDate = DateTime.UtcNow.AddDays(3).ToString("yyyy-MM-dd"),
                                    InstructionLines = new PagBankBoletoInstructions
                                    {
                                        Line1 = "Não receber após o vencimento",
                                        Line2 = "Em caso de dúvidas, entre em contato com o suporte"
                                    },
                                    Holder = new PagBankBoletoHolder
                                    {
                                        Name = holderName,
                                        TaxId = holderTaxId,
                                        Email = holderEmail,
                                        Address = holderAddress
                                    }
                                }
                            }
                        }
                    };
                }
                else if (method == PaymentMethod.CreditCard)
                {
                    if (card == null)
                        throw new ArgumentException("Cartão é obrigatório para pagamento com cartão de crédito");

                    orderRequest.Charges = new List<PagBankCharge>
                    {
                        new PagBankCharge
                        {
                            ReferenceId = referenceId,
                            Description = "Pagamento de Endereço Fiscal",
                            Amount = new PagBankAmount
                            {
                                Value = (int)(amount * 100),
                                Currency = "BRL"
                            },
                            PaymentMethod = new PagBankPaymentMethod
                            {
                                Type = "CREDIT_CARD",
                                Installments = installments ?? 1,
                                Capture = true,
                                Card = card
                            }
                        }
                    };
                }

                var request = await CreateAuthenticatedRequestAsync(HttpMethod.Post, "/orders");
                
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };
                
                // Converter para snake_case para a API do PagBank
                var json = JsonSerializer.Serialize(orderRequest, jsonOptions);
                // Converter de camelCase para snake_case
                json = json.Replace("\"referenceId\"", "\"reference_id\"")
                          .Replace("\"taxId\"", "\"tax_id\"")
                          .Replace("\"unitAmount\"", "\"unit_amount\"")
                          .Replace("\"postalCode\"", "\"postal_code\"")
                          .Replace("\"instructionLines\"", "\"instruction_lines\"")
                          .Replace("\"line1\"", "\"line_1\"")
                          .Replace("\"line2\"", "\"line_2\"")
                          .Replace("\"paymentMethod\"", "\"payment_method\"")
                          .Replace("\"securityCode\"", "\"security_code\"")
                          .Replace("\"dueDate\"", "\"due_date\"")
                          .Replace("\"expirationDate\"", "\"expiration_date\"")
                          .Replace("\"firstSixDigits\"", "\"first_six_digits\"")
                          .Replace("\"lastFourDigits\"", "\"last_four_digits\"")
                          .Replace("\"formattedBarcode\"", "\"formatted_barcode\"");
                
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Erro ao criar pedido PagBank: {response.StatusCode} - {responseContent}");
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        throw new InvalidOperationException("Token de acesso PagBank inválido ou expirado. Verifique as credenciais nas Configurações do Sistema.");
                    }
                    if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        throw new InvalidOperationException(ParsePagBankErrorToFriendlyMessage(responseContent));
                    }
                    throw new HttpRequestException($"Erro ao criar pedido PagBank: {response.StatusCode} - {responseContent}");
                }

                var orderResponse = JsonSerializer.Deserialize<OrderResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (orderResponse == null)
                    throw new InvalidOperationException("Resposta inválida da API PagBank");

                return orderResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao criar pedido PagBank para referência {referenceId}");
                throw;
            }
        }

        public async Task<OrderResponse> GetOrderAsync(string orderId)
        {
            try
            {
                var request = await CreateAuthenticatedRequestAsync(HttpMethod.Get, $"/orders/{orderId}");
                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Erro ao consultar pedido PagBank: {response.StatusCode} - {responseContent}");
                    throw new HttpRequestException($"Erro ao consultar pedido PagBank: {response.StatusCode}");
                }

                var orderResponse = JsonSerializer.Deserialize<OrderResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (orderResponse == null)
                    throw new InvalidOperationException("Resposta inválida da API PagBank");

                return orderResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao consultar pedido PagBank {orderId}");
                throw;
            }
        }

        public async Task<OrderResponse> CancelChargeAsync(string chargeId, decimal? amount = null)
        {
            try
            {
                var cancelRequest = new Dictionary<string, object>();
                if (amount.HasValue)
                {
                    cancelRequest["amount"] = new PagBankAmount
                    {
                        Value = (int)(amount.Value * 100),
                        Currency = "BRL"
                    };
                }

                var request = await CreateAuthenticatedRequestAsync(HttpMethod.Post, $"/charges/{chargeId}/cancel");
                request.Content = new StringContent(
                    JsonSerializer.Serialize(cancelRequest),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Erro ao cancelar cobrança PagBank: {response.StatusCode} - {responseContent}");
                    throw new HttpRequestException($"Erro ao cancelar cobrança PagBank: {response.StatusCode}");
                }

                // Retornar o pedido atualizado
                // Nota: A API pode retornar a charge cancelada, precisamos buscar o order completo
                var chargeResponse = JsonSerializer.Deserialize<PagBankChargeResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // Por enquanto, retornar null ou buscar o order
                // Isso depende da resposta da API
                return new OrderResponse();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao cancelar cobrança PagBank {chargeId}");
                throw;
            }
        }

        public async Task<bool> ValidateWebhookSignatureAsync(string payload, string signature)
        {
            try
            {
                var webhookSecret = await _systemConfigService.GetValueAsync("PagBank.WebhookSecret");
                if (string.IsNullOrEmpty(webhookSecret))
                {
                    _logger.LogWarning("Webhook secret não configurado. Validação ignorada.");
                    return true; // Se não estiver configurado, aceitar (para desenvolvimento)
                }

                // Implementar validação de assinatura do webhook
                // O PagBank envia um header com a assinatura que precisa ser validada
                // Por enquanto, retornar true (implementar validação real depois)
                // TODO: Implementar validação HMAC com o webhookSecret
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao validar assinatura do webhook");
                return false;
            }
        }

        /// <summary>
        /// Converte a resposta de erro 400 do PagBank (error_messages) em mensagens amigáveis para o usuário.
        /// </summary>
        private static string ParsePagBankErrorToFriendlyMessage(string responseContent)
        {
            var messages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var doc = JsonDocument.Parse(responseContent);
                var root = doc.RootElement;
                if (root.TryGetProperty("error_messages", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in arr.EnumerateArray())
                    {
                        var p = item.TryGetProperty("parameter_name", out var pn) ? pn.GetString() : null;
                        var msg = MapPagBankParameterToFriendly(p);
                        if (!string.IsNullOrEmpty(msg)) messages.Add(msg);
                    }
                }
            }
            catch { /* se não conseguir interpretar o JSON, usa a mensagem genérica */ }

            if (messages.Count > 0)
                return string.Join(" ", messages);
            
            // Mensagem genérica mais apropriada - pode ser dados do cliente ou do cartão
            return "Dados inválidos. Verifique se todos os dados obrigatórios estão preenchidos corretamente em 'Meu Perfil' (CPF/CNPJ, Nome, Email).";
        }

        private static string? MapPagBankParameterToFriendly(string? parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName)) return null;
            var s = parameterName.ToLowerInvariant();
            
            // Erros relacionados a dados do cliente (não do cartão)
            if (s.Contains("tax_id") || s.Contains("taxid") || s.Contains("cpf") || s.Contains("cnpj"))
                return "CPF ou CNPJ inválido. Verifique os dados em 'Meu Perfil'.";
            if (s.Contains("customer") && (s.Contains("name") || s.Contains("email")))
                return "Dados do cliente inválidos. Complete seu cadastro em 'Meu Perfil'.";
            if (s.Contains("email") && !s.Contains("card"))
                return "Email inválido. Verifique o email em 'Meu Perfil'.";
            if (s.Contains("phone"))
                return "Telefone inválido. Verifique o telefone em 'Meu Perfil'.";
            
            // Erros relacionados a cartão de crédito
            if (s.Contains("security_code")) return "Código de segurança (CVV) inválido. Confira os 3 ou 4 dígitos no verso do cartão.";
            if (s.Contains("number") && s.Contains("card")) return "Número do cartão inválido.";
            if (s.Contains("exp_month") || s.Contains("exp_year") || s.Contains("expiration")) return "Data de validade inválida. Confira mês e ano.";
            if (s.Contains("holder") || (s.Contains("name") && s.Contains("card"))) return "Nome no cartão inválido.";
            if (s.Contains("card")) return "Dados do cartão inválidos. Verifique número, validade, CVV e nome.";
            
            return null;
        }

        private PagBankPhone ParsePhone(string phone)
        {
            // Remover caracteres não numéricos
            var cleanPhone = phone.Replace("(", "").Replace(")", "").Replace(" ", "").Replace("-", "");
            
            if (cleanPhone.Length >= 10)
            {
                var area = cleanPhone.Substring(0, 2);
                var number = cleanPhone.Substring(2);
                return new PagBankPhone
                {
                    Country = "55",
                    Area = area,
                    Number = number
                };
            }

            return new PagBankPhone
            {
                Country = "55",
                Area = "00",
                Number = cleanPhone
            };
        }
    }
}
