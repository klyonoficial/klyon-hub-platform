using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.DTOs.PagBank;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
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
            var useSandbox = await _systemConfigService.GetValueAsync(PagBankConfigKey.UseSandbox.ToConfigKey());
            var isSandbox = KlyonHubPlatform.Domain.Core.Constants.StringBooleanValue.IsTrue(useSandbox);
            return isSandbox ? _sandboxBaseUrl : _productionBaseUrl;
        }
        private async Task<string> GetAccessTokenAsync()
        {
            var token = await _systemConfigService.GetValueAsync(PagBankConfigKey.AccessToken.ToConfigKey());
            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException($"Token de acesso PagBank não configurado. Configure em Configurações do Sistema > {PagBankConfigKey.AccessToken.ToConfigKey()}");
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
                            UnitAmount = (int)(amount * 100)
                        }
                    }
                };
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
                    string GetValueOrDefault(string? value, string defaultValue = DefaultDisplayStrings.NaoInformado)
                    {
                        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
                    }
                    var holderName = GetValueOrDefault(clientName, DefaultDisplayStrings.NaoInformado);
                    if (string.IsNullOrWhiteSpace(holderName) || holderName == DefaultDisplayStrings.NaoInformado)
                    {
                        holderName = "Cliente";
                    }
                    var holderTaxId = !string.IsNullOrWhiteSpace(clientTaxId)
                        ? clientTaxId.Replace(".", "").Replace("/", "").Replace("-", "").Trim()
                        : "00000000000";
                    if (holderTaxId.Length < 11)
                    {
                        holderTaxId = holderTaxId.PadLeft(11, '0');
                    }
                    var holderEmail = GetValueOrDefault(clientEmail, "noreply@klyon.com.br");
                    if (!holderEmail.Contains("@") || holderEmail.Length < 5)
                    {
                        holderEmail = "noreply@klyon.com.br";
                    }
                    var holderStreet = GetValueOrDefault(clientAddress?.Street, DefaultDisplayStrings.NaoInformado);
                    var holderNumber = GetValueOrDefault(clientAddress?.Number, "S/N");
                    var holderComplement = clientAddress?.Complement;
                    var holderLocality = GetValueOrDefault(clientAddress?.Locality, DefaultDisplayStrings.NaoInformado);
                    var holderCity = GetValueOrDefault(clientAddress?.City, DefaultDisplayStrings.NaoInformado);
                    var holderRegion = "SP";
                    if (!string.IsNullOrWhiteSpace(clientAddress?.RegionCode))
                    {
                        var regionTemp = clientAddress.RegionCode.Trim().ToUpper();
                        holderRegion = regionTemp.Length >= 2 ? regionTemp.Substring(0, 2) : "SP";
                    }
                    if (!BrazilianUfs.All.Contains(holderRegion))
                    {
                        holderRegion = "SP";
                    }
                    var holderCountry = GetValueOrDefault(clientAddress?.Country, "BRA");
                    if (holderCountry != "BRA")
                    {
                        holderCountry = "BRA";
                    }
                    var holderPostalCode = "00000000";
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
                var json = JsonSerializer.Serialize(orderRequest, jsonOptions);
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
                var chargeResponse = JsonSerializer.Deserialize<PagBankChargeResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
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
                var token = await _systemConfigService.GetValueAsync(PagBankConfigKey.AccessToken.ToConfigKey());
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Token de acesso não configurado. Validação de webhook ignorada.");
                    return true;
                }
                if (string.IsNullOrEmpty(signature))
                {
                    _logger.LogWarning("Assinatura não enviada no webhook. Aceitando requisição (para desenvolvimento).");
                    return true;
                }
                var dataToHash = $"{token}-{payload}";
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(dataToHash));
                var computedSignature = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                var isValid = string.Equals(computedSignature, signature, StringComparison.OrdinalIgnoreCase);
                if (!isValid)
                {
                    _logger.LogWarning($"Assinatura do webhook inválida. Esperado: {computedSignature.Substring(0, 20)}..., Recebido: {signature.Substring(0, Math.Min(20, signature.Length))}...");
                }
                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao validar assinatura do webhook");
                return false;
            }
        }
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
                        var code = item.TryGetProperty("code", out var c) ? c.GetString() : null;
                        var description = item.TryGetProperty("description", out var d) ? d.GetString() : null;
                        if ((!string.IsNullOrEmpty(description) && description.Contains("buyer email", StringComparison.OrdinalIgnoreCase) && description.Contains("merchant email", StringComparison.OrdinalIgnoreCase)))
                        {
                            messages.Add("O endereço de e-mail do comprador não pode ser igual ao endereço de e-mail do comerciante.");
                            continue;
                        }
                        var p = item.TryGetProperty("parameter_name", out var pn) ? pn.GetString() : null;
                        var msg = MapPagBankParameterToFriendly(p);
                        if (!string.IsNullOrEmpty(msg)) messages.Add(msg);
                    }
                }
            }
            catch { /* se não conseguir interpretar o JSON, usa a mensagem genérica */ }
            if (messages.Count > 0)
                return string.Join(" ", messages);
            return "Dados inválidos. Verifique se todos os dados obrigatórios estão preenchidos corretamente em 'Meu Perfil' (CPF/CNPJ, Nome, Email).";
        }
        private static string? MapPagBankParameterToFriendly(string? parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName)) return null;
            var s = parameterName.ToLowerInvariant();
            if (s.Contains("tax_id") || s.Contains("taxid") || s.Contains("cpf") || s.Contains("cnpj"))
                return "CPF ou CNPJ inválido. Verifique os dados em 'Meu Perfil'.";
            if (s.Contains("customer") && (s.Contains("name") || s.Contains("email")))
                return "Dados do cliente inválidos. Complete seu cadastro em 'Meu Perfil'.";
            if (s.Contains("email") && !s.Contains("card"))
                return "Email inválido. Verifique o email em 'Meu Perfil'.";
            if (s.Contains("phone"))
                return "Telefone inválido. Verifique o telefone em 'Meu Perfil'.";
            if (s.Contains("security_code")) return "Código de segurança (CVV) inválido. Confira os 3 ou 4 dígitos no verso do cartão.";
            if (s.Contains("number") && s.Contains("card")) return "Número do cartão inválido.";
            if (s.Contains("exp_month") || s.Contains("exp_year") || s.Contains("expiration")) return "Data de validade inválida. Confira mês e ano.";
            if (s.Contains("holder") || (s.Contains("name") && s.Contains("card"))) return "Nome no cartão inválido.";
            if (s.Contains("card")) return "Dados do cartão inválidos. Verifique número, validade, CVV e nome.";
            return null;
        }
        private PagBankPhone ParsePhone(string phone)
        {
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
        #region PIX Transfer
        public async Task<PixTransferResult> CreatePixTransferAsync(string referenceId, decimal amount, string pixKey, 
            string pixKeyType, string receiverName, string? description = null)
        {
            try
            {
                _logger.LogInformation("Criando transferência PIX. Ref: {ReferenceId}, Valor: {Amount}, Chave: {PixKey}",
                    referenceId, amount, pixKey.Substring(0, Math.Min(5, pixKey.Length)) + "***");
                var transferRequest = new
                {
                    reference_id = referenceId,
                    receiver = new
                    {
                        pix_key = pixKey,
                        pix_key_type = pixKeyType,
                        name = receiverName
                    },
                    amount = new
                    {
                        value = (int)(amount * 100),
                        currency = "BRL"
                    },
                    description = description ?? $"Repasse - Ref: {referenceId}"
                };
                var request = await CreateAuthenticatedRequestAsync(HttpMethod.Post, "/pix/transfers");
                request.Content = new StringContent(
                    JsonSerializer.Serialize(transferRequest),
                    Encoding.UTF8,
                    "application/json"
                );
                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Erro na transferência PIX PagBank: {StatusCode} - {Response}", 
                        response.StatusCode, responseContent);
                    return new PixTransferResult
                    {
                        Success = false,
                        ErrorMessage = $"Erro na transferência: {response.StatusCode}. Detalhes: {responseContent}"
                    };
                }
                using var doc = JsonDocument.Parse(responseContent);
                var root = doc.RootElement;
                var transferId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                var status = root.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
                var endToEndId = root.TryGetProperty("end_to_end_id", out var e2eProp) ? e2eProp.GetString() : null;
                return new PixTransferResult
                {
                    Success = true,
                    TransferId = transferId,
                    Status = status,
                    Amount = amount,
                    EndToEndId = endToEndId,
                    CreatedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar transferência PIX para {PixKey}", pixKey);
                return new PixTransferResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
        public async Task<PixTransferResult> GetPixTransferAsync(string transferId)
        {
            try
            {
                var request = await CreateAuthenticatedRequestAsync(HttpMethod.Get, $"/pix/transfers/{transferId}");
                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Erro ao consultar transferência PIX: {StatusCode} - {Response}",
                        response.StatusCode, responseContent);
                    return new PixTransferResult
                    {
                        Success = false,
                        TransferId = transferId,
                        ErrorMessage = $"Erro na consulta: {response.StatusCode}"
                    };
                }
                using var doc = JsonDocument.Parse(responseContent);
                var root = doc.RootElement;
                var status = root.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
                var endToEndId = root.TryGetProperty("end_to_end_id", out var e2eProp) ? e2eProp.GetString() : null;
                var amountValue = 0m;
                if (root.TryGetProperty("amount", out var amountProp) && 
                    amountProp.TryGetProperty("value", out var valueProp))
                {
                    amountValue = valueProp.GetInt32() / 100m;
                }
                return new PixTransferResult
                {
                    Success = true,
                    TransferId = transferId,
                    Status = status,
                    Amount = amountValue,
                    EndToEndId = endToEndId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao consultar transferência PIX {TransferId}", transferId);
                return new PixTransferResult
                {
                    Success = false,
                    TransferId = transferId,
                    ErrorMessage = ex.Message
                };
            }
        }
        #endregion
    }
}