using AutoMapper;
using KlyonHubPlatform.Application.DTOs.Asaas;
using KlyonHubPlatform.Application.DTOs.PagBank;
using KlyonHubPlatform.Application.Helpers;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
namespace KlyonHubPlatform.Application.Services
{
    public class AsaasService : IAsaasService
    {
        private const string SandboxBaseUrl = "https://api-sandbox.asaas.com/v3";
        private const string ProductionBaseUrl = "https://api.asaas.com/v3";
        private readonly ISystemConfigurationService _systemConfig;
        private readonly ILogger<AsaasService> _logger;
        private readonly HttpClient _httpClient;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        private readonly IMapper _mapper;
        public AsaasService(
            ISystemConfigurationService systemConfig,
            ILogger<AsaasService> logger,
            HttpClient httpClient,
            IMapper mapper)
        {
            _systemConfig = systemConfig;
            _logger = logger;
            _httpClient = httpClient;
            _mapper = mapper;
        }
        private async Task<string> GetBaseUrlAsync()
        {
            var useSandbox = await _systemConfig.GetValueAsync(AsaasConfigKey.UseSandbox.ToConfigKey());
            return StringBooleanValue.IsTrue(useSandbox) ? SandboxBaseUrl : ProductionBaseUrl;
        }
        private async Task<string> GetApiKeyAsync()
        {
            var key = await _systemConfig.GetValueAsync(AsaasConfigKey.ApiKey.ToConfigKey());
            if (string.IsNullOrEmpty(key))
                throw new InvalidOperationException(AsaasErrorMessages.ApiKeyNotConfigured);
            return key;
        }
        private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string path)
        {
            var apiKey = await GetApiKeyAsync();
            var baseUrl = await GetBaseUrlAsync();
            var request = new HttpRequestMessage(method, $"{baseUrl}{path}");
            request.Headers.Add("access_token", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("User-Agent", "KlyonHubPlatform/1.0");
            return request;
        }
        public AsaasValidationResult ValidateCustomerDataForPayment(
            string? clientName,
            string? clientEmail,
            string? clientTaxId,
            string? clientPhone,
            decimal value,
            string? dueDate = null)
        {
            var result = new AsaasValidationResult();
            if (string.IsNullOrWhiteSpace(clientName))
                result.Errors.Add("Nome do cliente é obrigatório (customerData.name).");
            if (string.IsNullOrWhiteSpace(clientEmail))
                result.Errors.Add("E-mail do cliente é obrigatório (customerData.email).");
            else if (!clientEmail.Contains("@") || clientEmail.Length < 5)
                result.Errors.Add("E-mail do cliente inválido (customerData.email).");
            var cpfCnpj = NormalizeCpfCnpj(clientTaxId);
            if (string.IsNullOrEmpty(cpfCnpj))
                result.Errors.Add("CPF ou CNPJ é obrigatório (customerData.cpfCnpj). Apenas dígitos (11 ou 14).");
            else if (cpfCnpj.Length != 11 && cpfCnpj.Length != 14)
                result.Errors.Add("CPF deve ter 11 dígitos ou CNPJ 14 dígitos, apenas números (customerData.cpfCnpj).");
            else if (cpfCnpj.Length == 11 && !IsValidCpf(cpfCnpj))
                result.Errors.Add("CPF com dígitos verificadores inválidos. Corrija em Meu Perfil (use um CPF válido).");
            else if (cpfCnpj.Length == 14 && !IsValidCnpj(cpfCnpj))
                result.Errors.Add("CNPJ com dígitos verificadores inválidos. Corrija em Meu Perfil (use um CNPJ válido).");
            if (value <= 0)
                result.Errors.Add("Valor da cobrança deve ser maior que zero (value).");
            if (!string.IsNullOrWhiteSpace(dueDate) && !System.Text.RegularExpressions.Regex.IsMatch(dueDate, @"^\d{4}-\d{2}-\d{2}$"))
                result.Errors.Add("Data de vencimento deve estar no formato yyyy-MM-dd (dueDate).");
            var phone = NormalizePhone(clientPhone);
            if (!string.IsNullOrWhiteSpace(clientPhone) && (phone == null || phone.Length < 10))
                result.Errors.Add("Telefone deve ter pelo menos 10 dígitos (customerData.phone/mobilePhone).");
            result.IsValid = result.Errors.Count == 0;
            result.Message = result.IsValid ? "Dados válidos conforme documentação Asaas." : string.Join(" ", result.Errors);
            return result;
        }
        public async Task<(bool Success, int StatusCode, string Message)> TestCreatePaymentAsync()
        {
            const string testCpf = "52998224725";
            var referenceId = $"TEST-PAY-{DateTime.UtcNow:yyyyMMddHHmmss}";
            var dueDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
            try
            {
                var customerPayload = new AsaasCustomerRequest
                {
                    Name = "Teste Validação Asaas",
                    CpfCnpj = testCpf,
                    Email = "teste@asaas.com.br",
                    Phone = "27987654321",
                    MobilePhone = "27987654321",
                    Address = "Rua Teste",
                    AddressNumber = "100",
                    PostalCode = "29010000",
                    Province = "Centro",
                    City = "Vitória",
                    State = "ES",
                    ExternalReference = referenceId
                };
                var customerReq = await CreateRequestAsync(HttpMethod.Post, "/customers");
                customerReq.Content = new StringContent(JsonSerializer.Serialize(customerPayload, JsonOptions), Encoding.UTF8, "application/json");
                var customerResponse = await _httpClient.SendAsync(customerReq);
                var customerContent = await customerResponse.Content.ReadAsStringAsync();
                if (!customerResponse.IsSuccessStatusCode)
                {
                    var errMsg = ParseAsaasError(customerContent);
                    return (false, (int)customerResponse.StatusCode, $"Cliente (customers): {errMsg}");
                }
                var customer = JsonSerializer.Deserialize<AsaasCustomerResponse>(customerContent, JsonOptions);
                var customerId = customer?.Id;
                if (string.IsNullOrEmpty(customerId))
                {
                    return (false, 0, "Resposta de criação de cliente sem id.");
                }
                var paymentPayload = new AsaasPaymentRequest
                {
                    Customer = customerId,
                    BillingType = "PIX",
                    Value = 5.00m,
                    DueDate = dueDate,
                    Description = "Teste de criação de cobrança (validação 200)",
                    ExternalReference = referenceId
                };
                var paymentReq = await CreateRequestAsync(HttpMethod.Post, "/payments");
                paymentReq.Content = new StringContent(JsonSerializer.Serialize(paymentPayload, JsonOptions), Encoding.UTF8, "application/json");
                var paymentResponse = await _httpClient.SendAsync(paymentReq);
                var paymentContent = await paymentResponse.Content.ReadAsStringAsync();
                var statusCode = (int)paymentResponse.StatusCode;
                if (paymentResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation("TestCreatePayment Asaas: Status {StatusCode}, customerId={CustomerId}.", statusCode, customerId);
                    return (true, statusCode, $"Retorno 200: cobrança de teste criada (cliente {customerId}).");
                }
                var payErr = ParseAsaasError(paymentContent);
                return (false, statusCode, $"Cobrança (payments): {payErr}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TestCreatePayment Asaas falhou.");
                return (false, 0, $"Erro: {ex.Message}");
            }
        }
        public async Task<(bool Success, int StatusCode, string Message)> TestCreateCreditCardPaymentAsync()
        {
            const string testCpf = "52998224725";
            const string testCardNumber = "4444444444444444";
            var referenceId = $"TEST-CARD-{DateTime.UtcNow:yyyyMMddHHmmss}";
            var dueDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
            try
            {
                var customerPayload = new AsaasCustomerRequest
                {
                    Name = "Teste Cartão Asaas",
                    CpfCnpj = testCpf,
                    Email = "teste@asaas.com.br",
                    Phone = "27987654321",
                    MobilePhone = "27987654321",
                    Address = "Rua Teste",
                    AddressNumber = "100",
                    PostalCode = "29010000",
                    Province = "Centro",
                    City = "Vitória",
                    State = "ES",
                    ExternalReference = referenceId
                };
                var customerReq = await CreateRequestAsync(HttpMethod.Post, "/customers");
                customerReq.Content = new StringContent(JsonSerializer.Serialize(customerPayload, JsonOptions), Encoding.UTF8, "application/json");
                var customerResponse = await _httpClient.SendAsync(customerReq);
                var customerContent = await customerResponse.Content.ReadAsStringAsync();
                if (!customerResponse.IsSuccessStatusCode)
                {
                    return (false, (int)customerResponse.StatusCode, $"Cliente (customers): {ParseAsaasError(customerContent)}");
                }
                var customer = JsonSerializer.Deserialize<AsaasCustomerResponse>(customerContent, JsonOptions);
                var customerId = customer?.Id;
                if (string.IsNullOrEmpty(customerId))
                {
                    return (false, 0, "Resposta de criação de cliente sem id.");
                }
                var expYear = (DateTime.UtcNow.Year + 2).ToString();
                var paymentPayload = new AsaasPaymentRequest
                {
                    Customer = customerId,
                    BillingType = "CREDIT_CARD",
                    Value = 5.00m,
                    DueDate = dueDate,
                    Description = "Teste de cobrança com cartão (Admin)",
                    ExternalReference = referenceId,
                    InstallmentCount = 1,
                    InstallmentValue = 5.00m,
                    CreditCard = new AsaasCreditCard
                    {
                        HolderName = "TESTE APROVADO",
                        Number = testCardNumber,
                        ExpiryMonth = "12",
                        ExpiryYear = expYear,
                        Ccv = "123"
                    },
                    CreditCardHolderInfo = new AsaasCreditCardHolderInfo
                    {
                        Name = "Teste Cartão Asaas",
                        Email = "teste@asaas.com.br",
                        CpfCnpj = testCpf,
                        Phone = "27987654321",
                        MobilePhone = "27987654321"
                    }
                };
                var paymentReq = await CreateRequestAsync(HttpMethod.Post, "/payments");
                paymentReq.Content = new StringContent(JsonSerializer.Serialize(paymentPayload, JsonOptions), Encoding.UTF8, "application/json");
                var paymentResponse = await _httpClient.SendAsync(paymentReq);
                var paymentContent = await paymentResponse.Content.ReadAsStringAsync();
                var statusCode = (int)paymentResponse.StatusCode;
                if (paymentResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation("TestCreateCreditCardPayment Asaas: Status {StatusCode}, customerId={CustomerId}.", statusCode, customerId);
                    return (true, statusCode, $"Retorno {statusCode}: cobrança com cartão de teste criada (cliente {customerId}). Use Sandbox para ver o pagamento aprovado.");
                }
                return (false, statusCode, $"Cobrança (payments): {ParseAsaasError(paymentContent)}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TestCreateCreditCardPayment Asaas falhou.");
                return (false, 0, $"Erro: {ex.Message}");
            }
        }
        public async Task<string> GetOrCreateAsaasCustomerIdAsync(string clientName, string clientEmail, string clientTaxId, string? clientPhone, PagBankAddress? clientAddress, string externalReference)
        {
            var cpfCnpj = NormalizeCpfCnpj(clientTaxId);
            if (string.IsNullOrEmpty(cpfCnpj) || (cpfCnpj.Length != 11 && cpfCnpj.Length != 14))
                throw new InvalidOperationException("CPF ou CNPJ inválido. Apenas dígitos (11 ou 14).");
            return await CreateOrGetAsaasCustomerAsync(clientName, clientEmail, cpfCnpj, clientPhone, clientAddress, externalReference);
        }

        public async Task<AsaasCustomerResponse?> GetCustomerAsync(string asaasCustomerId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(asaasCustomerId))
                return null;

            try
            {
                var httpRequest = await CreateRequestAsync(
                    HttpMethod.Get,
                    $"/customers/{Uri.EscapeDataString(asaasCustomerId.Trim())}");
                var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Asaas: falha ao obter customer {CustomerId}: {StatusCode} - {Content}",
                        asaasCustomerId, response.StatusCode, content);
                    return null;
                }

                return JsonSerializer.Deserialize<AsaasCustomerResponse>(content, JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Asaas: erro ao obter customer {CustomerId}.", asaasCustomerId);
                return null;
            }
        }

        public async Task<AsaasCustomerSyncOutcome> TryUpdateExistingCustomerAsync(
            string externalReference,
            string clientName,
            string? clientEmail,
            string? clientTaxId,
            string? clientPhone,
            PagBankAddress? clientAddress,
            string? asaasCustomerId = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var customerId = asaasCustomerId?.Trim();
                if (string.IsNullOrEmpty(customerId) && !string.IsNullOrWhiteSpace(externalReference))
                    customerId = await FindCustomerIdByExternalReferenceAsync(externalReference, cancellationToken);

                if (string.IsNullOrEmpty(customerId))
                {
                    _logger.LogInformation(
                        "Asaas: nenhum customer para atualizar (externalReference={ExternalReference}). Sync ignorado.",
                        externalReference);
                    return AsaasCustomerSyncOutcome.Skipped;
                }

                var cpfCnpj = NormalizeCpfCnpj(clientTaxId);
                if (!string.IsNullOrEmpty(cpfCnpj) && cpfCnpj.Length != 11 && cpfCnpj.Length != 14)
                    cpfCnpj = "";

                var phone = NormalizePhone(clientPhone);
                var payload = new AsaasCustomerRequest
                {
                    Name = (clientName ?? string.Empty).Trim(),
                    Email = string.IsNullOrWhiteSpace(clientEmail) ? null : clientEmail.Trim(),
                    CpfCnpj = string.IsNullOrEmpty(cpfCnpj) ? null : cpfCnpj,
                    Phone = phone,
                    MobilePhone = phone,
                    Address = NullIfBlankOrPlaceholder(clientAddress?.Street),
                    AddressNumber = NullIfBlankOrPlaceholder(clientAddress?.Number, treatSnAsPlaceholder: false),
                    Complement = NullIfBlankOrPlaceholder(clientAddress?.Complement),
                    Province = NullIfBlankOrPlaceholder(clientAddress?.Locality),
                    PostalCode = NormalizePostalCodeForUpdate(clientAddress?.PostalCode),
                    City = TryGetCityAsIbgeCode(clientAddress?.City)?.ToString() ?? NullIfBlankOrPlaceholder(clientAddress?.City),
                    State = NullIfBlankOrPlaceholder(clientAddress?.RegionCode),
                    ExternalReference = string.IsNullOrWhiteSpace(externalReference) ? null : externalReference.Trim()
                };

                if (string.IsNullOrWhiteSpace(payload.Name))
                {
                    _logger.LogInformation("Asaas: nome vazio ao sincronizar customer {CustomerId}. Sync ignorado.", customerId);
                    return AsaasCustomerSyncOutcome.Skipped;
                }

                var httpRequest = await CreateRequestAsync(HttpMethod.Put, $"/customers/{Uri.EscapeDataString(customerId)}");
                httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
                var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Asaas: falha ao atualizar customer {CustomerId}: {StatusCode} - {Content}",
                        customerId, response.StatusCode, content);
                    return AsaasCustomerSyncOutcome.Failed;
                }

                _logger.LogInformation("Asaas: customer {CustomerId} atualizado (externalReference={ExternalReference}).", customerId, externalReference);
                return AsaasCustomerSyncOutcome.Updated;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Asaas: erro ao sincronizar customer (externalReference={ExternalReference}).", externalReference);
                return AsaasCustomerSyncOutcome.Failed;
            }
        }

        public async Task<string?> TokenizeCreditCardAsync(string asaasCustomerId, string cardNumber, string expiryMonth, string expiryYear, string cvv, string holderName, string email, string cpfCnpj, string postalCode, string addressNumber, string? phone, string remoteIp)
        {
            var cardNumberDigits = (cardNumber ?? "").Replace(" ", "").Replace("-", "").Trim();
            var month = (expiryMonth ?? "").Trim();
            if (month.Length == 1) month = "0" + month;
            var year = (expiryYear ?? "").Trim();
            if (year.Length == 2) year = "20" + year;
            var phoneNorm = NormalizePhone(phone);
            var postal = NormalizePostalCode(postalCode)
                ?? throw new InvalidOperationException("Informe um CEP válido (8 dígitos). O CEP 00000-000 não é aceito.");
            var holderAddressNumber = string.IsNullOrWhiteSpace(addressNumber)
                ? throw new InvalidOperationException("Informe o número do endereço do titular do cartão.")
                : addressNumber.Trim();
            var body = new
            {
                customer = asaasCustomerId,
                creditCard = new
                {
                    holderName = (holderName ?? "").Trim(),
                    number = cardNumberDigits,
                    expiryMonth = month,
                    expiryYear = year,
                    ccv = (cvv ?? "").Trim()
                },
                creditCardHolderInfo = new
                {
                    name = (holderName ?? "").Trim(),
                    email = (email ?? "").Trim(),
                    cpfCnpj = NormalizeCpfCnpj(cpfCnpj),
                    postalCode = postal,
                    addressNumber = holderAddressNumber,
                    phone = phoneNorm ?? ""
                },
                remoteIp = (remoteIp ?? "127.0.0.1").Trim()
            };
            var httpRequest = await CreateRequestAsync(HttpMethod.Post, "/creditCard/tokenizeCreditCard");
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
            var response = await _httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Tokenização de cartão Asaas: {StatusCode} - {Content}", response.StatusCode, content);
                throw new InvalidOperationException(ParseAsaasCreditCardError(content));
            }
            var tokenResponse = JsonSerializer.Deserialize<AsaasTokenizeCardResponse>(content, JsonOptions);
            return tokenResponse?.CreditCardToken;
        }

        public async Task<OrderResponse> CreateOrderAsync(
            string referenceId,
            decimal amount,
            PaymentMethod method,
            string clientName,
            string clientEmail,
            string clientTaxId,
            string? clientPhone = null,
            PagBankCard? card = null,
            int? installments = null,
            PagBankAddress? clientAddress = null,
            string? creditCardToken = null,
            string? asaasCustomerIdForToken = null,
            string? remoteIp = null,
            string? customerExternalReference = null)
        {
            var dueDate = method == PaymentMethod.BankTransfer
                ? DateTime.UtcNow.AddDays(3).ToString("yyyy-MM-dd")
                : DateTime.UtcNow.ToString("yyyy-MM-dd");
            var cpfCnpj = NormalizeCpfCnpj(clientTaxId);
            if (string.IsNullOrEmpty(cpfCnpj) || (cpfCnpj.Length != 11 && cpfCnpj.Length != 14))
                throw new InvalidOperationException(
                    "CPF ou CNPJ inválido. Atualize em Meu Perfil com um CPF (11 dígitos) ou CNPJ (14 dígitos) válido, apenas números.");
            if (cpfCnpj.Length == 11 && !IsValidCpf(cpfCnpj))
                throw new InvalidOperationException(
                    "CPF com dígitos verificadores inválidos. Corrija o CPF em Meu Perfil (use um CPF válido).");
            if (cpfCnpj.Length == 14 && !IsValidCnpj(cpfCnpj))
                throw new InvalidOperationException(
                    "CNPJ com dígitos verificadores inválidos. Corrija o CNPJ em Meu Perfil (use um CNPJ válido).");
            string customerId;
            if (!string.IsNullOrEmpty(creditCardToken) && !string.IsNullOrEmpty(asaasCustomerIdForToken))
                customerId = asaasCustomerIdForToken;
            else
                customerId = await CreateOrGetAsaasCustomerAsync(clientName, clientEmail, cpfCnpj, clientPhone, clientAddress, !string.IsNullOrWhiteSpace(customerExternalReference) ? customerExternalReference.Trim() : referenceId);
            var request = new AsaasPaymentRequest
            {
                Customer = customerId,
                ExternalReference = referenceId,
                Value = amount,
                DueDate = dueDate,
                Description = "Pagamento de Endereço Fiscal"
            };
            if (method == PaymentMethod.PIX)
            {
                request.BillingType = "PIX";
            }
            else if (method == PaymentMethod.BankTransfer)
            {
                request.BillingType = "BOLETO";
            }
            else if (method == PaymentMethod.CreditCard)
            {
                request.BillingType = "CREDIT_CARD";
                // installmentCount só para parcelamento real (2+). Com 1 cria "Parcelamentos 1x" no Asaas
                // em vez de cobrança avulsa — e não é assinatura recorrente.
                if (installments.HasValue && installments.Value > 1)
                {
                    request.InstallmentCount = installments.Value;
                    request.InstallmentValue = Math.Round(amount / installments.Value, 2);
                }
                request.RemoteIp = string.IsNullOrEmpty(remoteIp) ? "127.0.0.1" : remoteIp.Trim();
                var holderPostalCode = NormalizePostalCode(clientAddress?.PostalCode);
                var holderAddressNumber = string.IsNullOrWhiteSpace(clientAddress?.Number) ? null : clientAddress.Number.Trim();
                if (string.IsNullOrEmpty(holderPostalCode) || string.IsNullOrEmpty(holderAddressNumber))
                    throw new InvalidOperationException("Informe o CEP e o número do endereço do titular do cartão.");
                if (!string.IsNullOrEmpty(creditCardToken))
                {
                    request.CreditCardToken = creditCardToken;
                }
                else if (card != null)
                {
                    request.CreditCard = MapCard(card);
                }
                request.CreditCardHolderInfo = new AsaasCreditCardHolderInfo
                {
                    Name = card?.Holder?.Name ?? clientName,
                    Email = clientEmail,
                    CpfCnpj = NormalizeCpfCnpj(clientTaxId),
                    PostalCode = holderPostalCode,
                    AddressNumber = holderAddressNumber,
                    Phone = NormalizePhone(clientPhone),
                    MobilePhone = NormalizePhone(clientPhone)
                };
            }
            if (method == PaymentMethod.CreditCard)
            {
                _logger.LogInformation(
                    "Enviando cobrança com cartão ao Asaas: referenceId={ReferenceId}, value={Value}, customerId={CustomerId}.",
                    referenceId, amount, request.Customer);
            }
            var httpRequest = await CreateRequestAsync(HttpMethod.Post, "/payments");
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json");
            var response = await _httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Erro ao criar cobrança Asaas: {StatusCode} - {Content}", response.StatusCode, content);
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new InvalidOperationException("API Key Asaas inválida ou expirada. Verifique nas Configurações do Sistema.");
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    throw new InvalidOperationException(method == PaymentMethod.CreditCard
                        ? ParseAsaasCreditCardError(content)
                        : ParseAsaasError(content));
                throw new HttpRequestException($"Erro ao criar cobrança Asaas: {response.StatusCode}");
            }
            var payment = JsonSerializer.Deserialize<AsaasPaymentResponse>(content, JsonOptions);
            if (payment == null)
                throw new InvalidOperationException("Resposta inválida da API Asaas");
            var orderResponse = new OrderResponse
            {
                Id = payment.Id,
                ReferenceId = referenceId,
                Status = payment.Status,
                CreatedAt = AsaasDateParser.TryParse(payment.DateCreated) ?? DateTime.UtcNow,
                PaidAt = AsaasDateParser.TryParse(payment.ConfirmedDate ?? payment.ClientPaymentDate)
            };
            if (method == PaymentMethod.PIX)
            {
                var pixQr = await GetPixQrCodeAsync(payment.Id);
                if (pixQr != null)
                {
                    orderResponse.QrCodes = new List<PagBankQrCodeResponse>
                    {
                        new PagBankQrCodeResponse
                        {
                            Id = payment.Id,
                            Text = pixQr.Payload ?? pixQr.EncodedImage,
                            ExpirationDate = AsaasDateParser.TryParse(pixQr.ExpirationDate),
                            Amount = new PagBankAmount { Value = (int)(amount * 100), Currency = "BRL" }
                        }
                    };
                }
            }
            else if (method == PaymentMethod.BankTransfer)
            {
                var identification = await GetIdentificationFieldAsync(payment.Id);
                orderResponse.Charges = new List<PagBankChargeResponse>
                {
                    new PagBankChargeResponse
                    {
                        Id = payment.Id,
                        ReferenceId = referenceId,
                        Status = payment.Status,
                        CreatedAt = orderResponse.CreatedAt,
                        PaidAt = orderResponse.PaidAt,
                        Amount = new PagBankAmount { Value = (int)(amount * 100), Currency = "BRL" },
                        PaymentMethod = new PagBankPaymentMethodResponse
                        {
                            Type = "BOLETO",
                            Boleto = new PagBankBoletoResponse
                            {
                                Id = payment.Id,
                                FormattedBarcode = identification?.IdentificationField,
                                Barcode = !string.IsNullOrEmpty(identification?.BarCode)
                                    ? identification.BarCode
                                    : identification?.IdentificationField,
                                DueDate = AsaasDateParser.TryParse(payment.DueDate),
                                Links = !string.IsNullOrEmpty(payment.BankSlipUrl)
                                    ? new List<PagBankLink> { new PagBankLink { Rel = "bankSlip", Href = payment.BankSlipUrl } }
                                    : null
                            }
                        }
                    }
                };
            }
            else if (method == PaymentMethod.CreditCard && payment.Status != null)
            {
                var statusUpper = payment.Status.ToUpperInvariant();
                var paid = statusUpper == AsaasPaymentStatus.Confirmed || statusUpper == AsaasPaymentStatus.Received;
                orderResponse.Charges = new List<PagBankChargeResponse>
                {
                    new PagBankChargeResponse
                    {
                        Id = payment.Id,
                        ReferenceId = referenceId,
                        Status = paid ? "PAID" : payment.Status,
                        CreatedAt = orderResponse.CreatedAt,
                        PaidAt = paid ? orderResponse.PaidAt ?? DateTime.UtcNow : null,
                        Amount = new PagBankAmount { Value = (int)(amount * 100), Currency = "BRL" },
                        PaymentMethod = new PagBankPaymentMethodResponse { Type = "CREDIT_CARD" }
                    }
                };
            }
            return orderResponse;
        }

        public async Task<OrderResponse> CreateCreditCardSubscriptionAsync(
            string referenceId,
            decimal amount,
            string cycle,
            string clientName,
            string clientEmail,
            string clientTaxId,
            string? clientPhone = null,
            PagBankCard? card = null,
            PagBankAddress? clientAddress = null,
            string? creditCardToken = null,
            string? asaasCustomerIdForToken = null,
            string? remoteIp = null,
            string? customerExternalReference = null)
        {
            var normalizedCycle = (cycle ?? "").Trim().ToUpperInvariant();
            if (normalizedCycle != AsaasSubscriptionCycle.Monthly && normalizedCycle != AsaasSubscriptionCycle.Yearly)
                throw new ArgumentException("Ciclo de assinatura Asaas inválido. Use MONTHLY ou YEARLY.", nameof(cycle));
            if (amount <= 0)
                throw new InvalidOperationException("Valor da assinatura deve ser maior que zero.");

            var cpfCnpj = NormalizeCpfCnpj(clientTaxId);
            if (string.IsNullOrEmpty(cpfCnpj) || (cpfCnpj.Length != 11 && cpfCnpj.Length != 14))
                throw new InvalidOperationException(
                    "CPF ou CNPJ inválido. Atualize em Meu Perfil com um CPF (11 dígitos) ou CNPJ (14 dígitos) válido, apenas números.");
            if (cpfCnpj.Length == 11 && !IsValidCpf(cpfCnpj))
                throw new InvalidOperationException(
                    "CPF com dígitos verificadores inválidos. Corrija o CPF em Meu Perfil (use um CPF válido).");
            if (cpfCnpj.Length == 14 && !IsValidCnpj(cpfCnpj))
                throw new InvalidOperationException(
                    "CNPJ com dígitos verificadores inválidos. Corrija o CNPJ em Meu Perfil (use um CNPJ válido).");

            string customerId;
            if (!string.IsNullOrEmpty(creditCardToken) && !string.IsNullOrEmpty(asaasCustomerIdForToken))
                customerId = asaasCustomerIdForToken;
            else
                customerId = await CreateOrGetAsaasCustomerAsync(
                    clientName,
                    clientEmail,
                    cpfCnpj,
                    clientPhone,
                    clientAddress,
                    !string.IsNullOrWhiteSpace(customerExternalReference) ? customerExternalReference.Trim() : referenceId);

            var holderPostalCode = NormalizePostalCode(clientAddress?.PostalCode);
            var holderAddressNumber = string.IsNullOrWhiteSpace(clientAddress?.Number) ? null : clientAddress.Number.Trim();
            if (string.IsNullOrEmpty(holderPostalCode) || string.IsNullOrEmpty(holderAddressNumber))
                throw new InvalidOperationException("Informe o CEP e o número do endereço do titular do cartão.");

            // Vencimento no fuso de Brasília: evita nextDueDate “amanhã/ontem” por UTC e atraso na 1ª cobrança.
            var nextDueDate = BrazilDateTime.Today.ToString("yyyy-MM-dd");
            var request = new AsaasSubscriptionRequest
            {
                Customer = customerId,
                BillingType = "CREDIT_CARD",
                Cycle = normalizedCycle,
                Value = amount,
                NextDueDate = nextDueDate,
                Description = normalizedCycle == AsaasSubscriptionCycle.Monthly
                    ? "Assinatura mensal - Endereço Fiscal"
                    : "Assinatura anual - Endereço Fiscal",
                ExternalReference = referenceId,
                RemoteIp = string.IsNullOrEmpty(remoteIp) ? "127.0.0.1" : remoteIp.Trim(),
                CreditCardHolderInfo = new AsaasCreditCardHolderInfo
                {
                    Name = card?.Holder?.Name ?? clientName,
                    Email = clientEmail,
                    CpfCnpj = cpfCnpj,
                    PostalCode = holderPostalCode,
                    AddressNumber = holderAddressNumber,
                    Phone = NormalizePhone(clientPhone),
                    MobilePhone = NormalizePhone(clientPhone)
                }
            };
            if (!string.IsNullOrEmpty(creditCardToken))
                request.CreditCardToken = creditCardToken;
            else if (card != null)
                request.CreditCard = MapCard(card);

            _logger.LogInformation(
                "Enviando assinatura com cartão ao Asaas: referenceId={ReferenceId}, cycle={Cycle}, value={Value}, customerId={CustomerId}.",
                referenceId, normalizedCycle, amount, customerId);

            var httpRequest = await CreateRequestAsync(HttpMethod.Post, "/subscriptions");
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json");
            var response = await _httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Erro ao criar assinatura Asaas: {StatusCode} - {Content}", response.StatusCode, content);
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new InvalidOperationException("API Key Asaas inválida ou expirada. Verifique nas Configurações do Sistema.");
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    throw new InvalidOperationException(ParseAsaasCreditCardError(content));
                throw new HttpRequestException($"Erro ao criar assinatura Asaas: {response.StatusCode}");
            }

            var subscription = JsonSerializer.Deserialize<AsaasSubscriptionResponse>(content, JsonOptions);
            if (subscription == null || string.IsNullOrEmpty(subscription.Id))
                throw new InvalidOperationException("Resposta inválida da API Asaas ao criar assinatura.");

            // A 1ª cobrança pode demorar milissegundos a aparecer; com cartão, ACTIVE = autorização ok.
            var firstPayment = await WaitForFirstSubscriptionPaymentAsync(subscription.Id);
            var subStatus = (subscription.Status ?? "").Trim().ToUpperInvariant();
            var paymentStatus = (firstPayment?.Status ?? "").Trim().ToUpperInvariant();
            var paidAt = AsaasDateParser.TryParse(firstPayment?.ConfirmedDate ?? firstPayment?.ClientPaymentDate);

            var declined = IsAsaasCreditCardDeclined(paymentStatus) || subStatus == AsaasSubscriptionStatus.Inactive;
            var paid = !declined && (
                IsAsaasCreditCardPaid(paymentStatus)
                || subStatus == AsaasSubscriptionStatus.Active);

            if (declined)
            {
                _logger.LogWarning(
                    "Assinatura Asaas criada sem cobrança aprovada: subscriptionId={SubscriptionId}, subStatus={SubStatus}, paymentStatus={PaymentStatus}.",
                    subscription.Id, subStatus, paymentStatus);
            }
            else if (paid && !IsAsaasCreditCardPaid(paymentStatus))
            {
                _logger.LogInformation(
                    "Assinatura Asaas ACTIVE tratada como paga (1ª cobrança ainda {PaymentStatus}): subscriptionId={SubscriptionId}.",
                    string.IsNullOrEmpty(paymentStatus) ? "(vazia)" : paymentStatus, subscription.Id);
            }

            var paymentId = firstPayment?.Id ?? subscription.Id;
            var chargeStatus = declined ? "DECLINED" : paid ? "PAID" : (string.IsNullOrEmpty(paymentStatus) ? "PENDING" : paymentStatus);

            return new OrderResponse
            {
                Id = paymentId,
                ReferenceId = referenceId,
                Status = chargeStatus,
                CreatedAt = AsaasDateParser.TryParse(subscription.DateCreated) ?? DateTime.UtcNow,
                PaidAt = paid ? paidAt ?? DateTime.UtcNow : paidAt,
                SubscriptionId = subscription.Id,
                Charges = new List<PagBankChargeResponse>
                {
                    new PagBankChargeResponse
                    {
                        Id = paymentId,
                        ReferenceId = referenceId,
                        Status = chargeStatus,
                        CreatedAt = AsaasDateParser.TryParse(firstPayment?.DateCreated) ?? DateTime.UtcNow,
                        PaidAt = paid ? paidAt ?? DateTime.UtcNow : null,
                        Amount = new PagBankAmount { Value = (int)(amount * 100), Currency = "BRL" },
                        PaymentMethod = new PagBankPaymentMethodResponse { Type = "CREDIT_CARD" }
                    }
                }
            };
        }

        public async Task<OrderResponse> CreateSubscriptionAsync(
            string referenceId,
            decimal amount,
            string billingType,
            string cycle,
            string clientName,
            string clientEmail,
            string clientTaxId,
            string? clientPhone = null,
            PagBankAddress? clientAddress = null,
            string? customerExternalReference = null)
        {
            var normalizedBilling = (billingType ?? "").Trim().ToUpperInvariant();
            if (normalizedBilling != AsaasBillingType.Pix && normalizedBilling != AsaasBillingType.Boleto)
                throw new ArgumentException("billingType de assinatura deve ser PIX ou BOLETO.", nameof(billingType));

            var normalizedCycle = (cycle ?? "").Trim().ToUpperInvariant();
            if (normalizedCycle != AsaasSubscriptionCycle.Monthly && normalizedCycle != AsaasSubscriptionCycle.Yearly)
                throw new ArgumentException("Ciclo de assinatura Asaas inválido. Use MONTHLY ou YEARLY.", nameof(cycle));
            if (amount <= 0)
                throw new InvalidOperationException("Valor da assinatura deve ser maior que zero.");

            var cpfCnpj = NormalizeCpfCnpj(clientTaxId);
            if (string.IsNullOrEmpty(cpfCnpj) || (cpfCnpj.Length != 11 && cpfCnpj.Length != 14))
                throw new InvalidOperationException(
                    "CPF ou CNPJ inválido. Atualize em Meu Perfil com um CPF (11 dígitos) ou CNPJ (14 dígitos) válido, apenas números.");
            if (cpfCnpj.Length == 11 && !IsValidCpf(cpfCnpj))
                throw new InvalidOperationException(
                    "CPF com dígitos verificadores inválidos. Corrija o CPF em Meu Perfil (use um CPF válido).");
            if (cpfCnpj.Length == 14 && !IsValidCnpj(cpfCnpj))
                throw new InvalidOperationException(
                    "CNPJ com dígitos verificadores inválidos. Corrija o CNPJ em Meu Perfil (use um CNPJ válido).");

            var customerId = await CreateOrGetAsaasCustomerAsync(
                clientName,
                clientEmail,
                cpfCnpj,
                clientPhone,
                clientAddress,
                !string.IsNullOrWhiteSpace(customerExternalReference) ? customerExternalReference.Trim() : referenceId);

            var nextDueDate = BrazilDateTime.Today.ToString("yyyy-MM-dd");
            var billingLabel = normalizedBilling == AsaasBillingType.Pix ? "PIX" : "Boleto";
            var request = new AsaasSubscriptionRequest
            {
                Customer = customerId,
                BillingType = normalizedBilling,
                Cycle = normalizedCycle,
                Value = amount,
                NextDueDate = nextDueDate,
                Description = normalizedCycle == AsaasSubscriptionCycle.Monthly
                    ? $"Assinatura mensal ({billingLabel}) - Endereço Fiscal"
                    : $"Assinatura anual ({billingLabel}) - Endereço Fiscal",
                ExternalReference = referenceId
            };

            _logger.LogInformation(
                "Enviando assinatura {BillingType} ao Asaas: referenceId={ReferenceId}, cycle={Cycle}, value={Value}, customerId={CustomerId}.",
                normalizedBilling, referenceId, normalizedCycle, amount, customerId);

            var httpRequest = await CreateRequestAsync(HttpMethod.Post, "/subscriptions");
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json");
            var response = await _httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Erro ao criar assinatura Asaas ({BillingType}): {StatusCode} - {Content}",
                    normalizedBilling, response.StatusCode, content);
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new InvalidOperationException("API Key Asaas inválida ou expirada. Verifique nas Configurações do Sistema.");
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    throw new InvalidOperationException(ParseAsaasError(content));
                throw new HttpRequestException($"Erro ao criar assinatura Asaas: {response.StatusCode}");
            }

            var subscription = JsonSerializer.Deserialize<AsaasSubscriptionResponse>(content, JsonOptions);
            if (subscription == null || string.IsNullOrEmpty(subscription.Id))
                throw new InvalidOperationException("Resposta inválida da API Asaas ao criar assinatura.");

            var firstPayment = await WaitForFirstSubscriptionPaymentAsync(subscription.Id, acceptAnyStatus: true);
            var paymentId = firstPayment?.Id ?? subscription.Id;
            var paymentStatus = (firstPayment?.Status ?? "").Trim().ToUpperInvariant();
            var chargeStatus = string.IsNullOrEmpty(paymentStatus) ? "PENDING" : paymentStatus;

            var orderResponse = new OrderResponse
            {
                Id = paymentId,
                ReferenceId = referenceId,
                Status = chargeStatus,
                CreatedAt = AsaasDateParser.TryParse(subscription.DateCreated) ?? DateTime.UtcNow,
                PaidAt = AsaasDateParser.TryParse(firstPayment?.ConfirmedDate ?? firstPayment?.ClientPaymentDate),
                SubscriptionId = subscription.Id
            };

            if (normalizedBilling == AsaasBillingType.Pix && !string.IsNullOrEmpty(firstPayment?.Id))
            {
                var pixQr = await GetPixQrCodeAsync(firstPayment.Id);
                if (pixQr != null)
                {
                    orderResponse.QrCodes = new List<PagBankQrCodeResponse>
                    {
                        new PagBankQrCodeResponse
                        {
                            Id = firstPayment.Id,
                            Text = pixQr.Payload ?? pixQr.EncodedImage,
                            ExpirationDate = AsaasDateParser.TryParse(pixQr.ExpirationDate),
                            Amount = new PagBankAmount { Value = (int)(amount * 100), Currency = "BRL" }
                        }
                    };
                }
            }
            else if (normalizedBilling == AsaasBillingType.Boleto && !string.IsNullOrEmpty(firstPayment?.Id))
            {
                var identification = await GetIdentificationFieldAsync(firstPayment.Id);
                orderResponse.Charges = new List<PagBankChargeResponse>
                {
                    new PagBankChargeResponse
                    {
                        Id = firstPayment.Id,
                        ReferenceId = referenceId,
                        Status = chargeStatus,
                        CreatedAt = AsaasDateParser.TryParse(firstPayment.DateCreated) ?? DateTime.UtcNow,
                        Amount = new PagBankAmount { Value = (int)(amount * 100), Currency = "BRL" },
                        PaymentMethod = new PagBankPaymentMethodResponse
                        {
                            Type = "BOLETO",
                            Boleto = new PagBankBoletoResponse
                            {
                                Id = firstPayment.Id,
                                FormattedBarcode = identification?.IdentificationField,
                                Barcode = !string.IsNullOrEmpty(identification?.BarCode)
                                    ? identification.BarCode
                                    : identification?.IdentificationField,
                                DueDate = AsaasDateParser.TryParse(firstPayment.DueDate),
                                Links = !string.IsNullOrEmpty(firstPayment.BankSlipUrl)
                                    ? new List<PagBankLink> { new PagBankLink { Rel = "bankSlip", Href = firstPayment.BankSlipUrl } }
                                    : null
                            }
                        }
                    }
                };
            }

            return orderResponse;
        }

        public async Task<AsaasSubscriptionResponse> UpdateSubscriptionAsync(
            string asaasSubscriptionId,
            string? billingType = null,
            decimal? value = null,
            string? cycle = null,
            bool updatePendingPayments = true,
            PagBankCard? card = null,
            AsaasCreditCardHolderInfo? creditCardHolderInfo = null,
            string? creditCardToken = null,
            string? remoteIp = null)
        {
            if (string.IsNullOrWhiteSpace(asaasSubscriptionId))
                throw new ArgumentException("Id da assinatura Asaas é obrigatório.", nameof(asaasSubscriptionId));

            var request = new AsaasSubscriptionUpdateRequest
            {
                UpdatePendingPayments = updatePendingPayments
            };

            if (!string.IsNullOrWhiteSpace(billingType))
            {
                var normalizedBilling = billingType.Trim().ToUpperInvariant();
                if (normalizedBilling is not (AsaasBillingType.CreditCard or AsaasBillingType.Pix or AsaasBillingType.Boleto))
                    throw new ArgumentException("billingType inválido. Use CREDIT_CARD, PIX ou BOLETO.", nameof(billingType));
                request.BillingType = normalizedBilling;
            }

            if (value.HasValue)
            {
                if (value.Value <= 0)
                    throw new InvalidOperationException("Valor da assinatura deve ser maior que zero.");
                request.Value = value.Value;
            }

            if (!string.IsNullOrWhiteSpace(cycle))
            {
                var normalizedCycle = cycle.Trim().ToUpperInvariant();
                if (normalizedCycle != AsaasSubscriptionCycle.Monthly && normalizedCycle != AsaasSubscriptionCycle.Yearly)
                    throw new ArgumentException("Ciclo inválido. Use MONTHLY ou YEARLY.", nameof(cycle));
                request.Cycle = normalizedCycle;
            }

            // Atualiza description para não ficar "Assinatura mensal (PIX)..." após troca para BOLETO.
            if (!string.IsNullOrWhiteSpace(request.BillingType) || !string.IsNullOrWhiteSpace(request.Cycle))
            {
                var billingLabel = (request.BillingType ?? "").Trim().ToUpperInvariant() switch
                {
                    AsaasBillingType.Boleto => "Boleto",
                    AsaasBillingType.Pix => "PIX",
                    AsaasBillingType.CreditCard => "Cartão",
                    _ => "Assinatura"
                };
                var cycleLabel = string.Equals(request.Cycle, AsaasSubscriptionCycle.Yearly, StringComparison.OrdinalIgnoreCase)
                    ? "anual"
                    : "mensal";
                request.Description = $"Assinatura {cycleLabel} ({billingLabel}) - Endereço Fiscal";
            }

            if (!string.IsNullOrEmpty(creditCardToken))
                request.CreditCardToken = creditCardToken;
            else if (card != null)
                request.CreditCard = MapCard(card);

            if (creditCardHolderInfo != null)
                request.CreditCardHolderInfo = creditCardHolderInfo;

            if (!string.IsNullOrEmpty(remoteIp) || request.CreditCard != null || !string.IsNullOrEmpty(request.CreditCardToken))
                request.RemoteIp = string.IsNullOrEmpty(remoteIp) ? "127.0.0.1" : remoteIp.Trim();

            _logger.LogInformation(
                "Atualizando assinatura Asaas {SubscriptionId}: billingType={BillingType}, value={Value}, cycle={Cycle}, updatePendingPayments={UpdatePending}.",
                asaasSubscriptionId, request.BillingType, request.Value, request.Cycle, updatePendingPayments);

            var httpRequest = await CreateRequestAsync(
                HttpMethod.Put,
                $"/subscriptions/{Uri.EscapeDataString(asaasSubscriptionId.Trim())}");
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json");
            var response = await _httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Erro ao atualizar assinatura Asaas {SubscriptionId}: {StatusCode} - {Content}",
                    asaasSubscriptionId, response.StatusCode, content);
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new InvalidOperationException("API Key Asaas inválida ou expirada. Verifique nas Configurações do Sistema.");
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    throw new InvalidOperationException(ParseAsaasError(content));
                throw new HttpRequestException($"Erro ao atualizar assinatura Asaas: {response.StatusCode}");
            }

            var updated = JsonSerializer.Deserialize<AsaasSubscriptionResponse>(content, JsonOptions);
            if (updated == null || string.IsNullOrEmpty(updated.Id))
                throw new InvalidOperationException("Resposta inválida da API Asaas ao atualizar assinatura.");
            return updated;
        }

        public async Task CancelSubscriptionAsync(string asaasSubscriptionId)
        {
            if (string.IsNullOrWhiteSpace(asaasSubscriptionId))
                throw new ArgumentException("Id da assinatura Asaas é obrigatório.", nameof(asaasSubscriptionId));

            var httpRequest = await CreateRequestAsync(
                HttpMethod.Delete,
                $"/subscriptions/{Uri.EscapeDataString(asaasSubscriptionId.Trim())}");
            var response = await _httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Erro ao cancelar assinatura Asaas {SubscriptionId}: {StatusCode} - {Content}",
                    asaasSubscriptionId, response.StatusCode, content);
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new InvalidOperationException("API Key Asaas inválida ou expirada. Verifique nas Configurações do Sistema.");
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    throw new InvalidOperationException(ParseAsaasError(content));
                throw new HttpRequestException($"Erro ao cancelar assinatura Asaas: {response.StatusCode}");
            }

            _logger.LogInformation("Assinatura Asaas {SubscriptionId} cancelada.", asaasSubscriptionId);
        }

        /// <inheritdoc />
        public async Task<string?> ResolveSubscriptionPendingPaymentIdAsync(
            string asaasSubscriptionId,
            string? preferredPaymentId = null)
        {
            if (string.IsNullOrWhiteSpace(asaasSubscriptionId))
                return null;

            var preferred = preferredPaymentId?.Trim();
            if (!string.IsNullOrEmpty(preferred)
                && preferred.StartsWith("pay_", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var existing = await GetOrderAsync(preferred);
                    var status = (existing.Status ?? "").Trim().ToUpperInvariant();
                    if (!IsAsaasCreditCardDeclined(status) && status != AsaasPaymentStatus.Deleted)
                        return preferred;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "pay_* preferido {PaymentId} indisponível na assinatura {SubscriptionId}; listando cobranças.",
                        preferred, asaasSubscriptionId);
                }
            }

            for (var attempt = 1; attempt <= 5; attempt++)
            {
                var candidate = await GetPreferredSubscriptionPaymentAsync(asaasSubscriptionId.Trim(), preferBoleto: false);
                if (candidate != null && !string.IsNullOrEmpty(candidate.Id))
                    return candidate.Id;
                if (attempt < 5)
                    await Task.Delay(400);
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<OrderResponse> PayExistingPaymentWithCreditCardAsync(
            string asaasPaymentId,
            PagBankCard? card = null,
            AsaasCreditCardHolderInfo? creditCardHolderInfo = null,
            string? creditCardToken = null,
            string? remoteIp = null)
        {
            if (string.IsNullOrWhiteSpace(asaasPaymentId))
                throw new ArgumentException("Id da cobrança Asaas é obrigatório.", nameof(asaasPaymentId));

            var payId = asaasPaymentId.Trim();

            // Já liquidada (CONFIRMED/RECEIVED): não tenta pagar de novo.
            try
            {
                var current = await GetOrderAsync(payId);
                var currentStatus = (current.Status ?? "").Trim().ToUpperInvariant();
                if (IsAsaasCreditCardPaid(currentStatus))
                {
                    _logger.LogInformation(
                        "Cobrança Asaas {PaymentId} já está {Status}; payWithCreditCard desnecessário.",
                        payId, currentStatus);
                    return MapCreditCardPaymentOrder(payId, "PAID", current.PaidAt ?? DateTime.UtcNow, current.ReferenceId, current.CreatedAt);
                }
                if (IsAsaasCreditCardDeclined(currentStatus))
                {
                    return MapCreditCardPaymentOrder(payId, "DECLINED", null, current.ReferenceId, current.CreatedAt);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao pré-consultar cobrança {PaymentId} antes de payWithCreditCard.", payId);
            }

            if (string.IsNullOrEmpty(creditCardToken) && card == null)
                throw new InvalidOperationException("Informe os dados do cartão ou um creditCardToken para pagar a cobrança.");

            var request = new AsaasPayWithCreditCardRequest
            {
                CreditCardToken = string.IsNullOrEmpty(creditCardToken) ? null : creditCardToken,
                CreditCard = string.IsNullOrEmpty(creditCardToken) && card != null ? MapCard(card) : null,
                CreditCardHolderInfo = string.IsNullOrEmpty(creditCardToken) ? creditCardHolderInfo : null,
                RemoteIp = string.IsNullOrEmpty(remoteIp) ? "127.0.0.1" : remoteIp.Trim()
            };

            _logger.LogInformation(
                "Captura imediata Asaas payWithCreditCard: paymentId={PaymentId}, hasToken={HasToken}.",
                payId, !string.IsNullOrEmpty(creditCardToken));

            var httpRequest = await CreateRequestAsync(
                HttpMethod.Post,
                $"/payments/{Uri.EscapeDataString(payId)}/payWithCreditCard");
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json");
            var response = await _httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Erro payWithCreditCard Asaas {PaymentId}: {StatusCode} - {Content}",
                    payId, response.StatusCode, content);
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new InvalidOperationException("API Key Asaas inválida ou expirada. Verifique nas Configurações do Sistema.");
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    throw new InvalidOperationException(ParseAsaasCreditCardError(content));
                throw new HttpRequestException($"Erro ao pagar cobrança com cartão no Asaas: {response.StatusCode}");
            }

            var payment = JsonSerializer.Deserialize<AsaasPaymentResponse>(content, JsonOptions);
            if (payment == null || string.IsNullOrEmpty(payment.Id))
                throw new InvalidOperationException("Resposta inválida da API Asaas ao pagar com cartão.");

            var paymentStatus = (payment.Status ?? "").Trim().ToUpperInvariant();
            var paidAt = AsaasDateParser.TryParse(payment.ConfirmedDate ?? payment.ClientPaymentDate);
            var declined = IsAsaasCreditCardDeclined(paymentStatus);
            var paid = !declined && IsAsaasCreditCardPaid(paymentStatus);
            var chargeStatus = declined ? "DECLINED" : paid ? "PAID" : (string.IsNullOrEmpty(paymentStatus) ? "PENDING" : paymentStatus);

            _logger.LogInformation(
                "payWithCreditCard Asaas concluído: paymentId={PaymentId}, asaasStatus={AsaasStatus}, mappedStatus={MappedStatus}.",
                payment.Id, paymentStatus, chargeStatus);

            // Se ainda PENDING (ex.: AWAITING_RISK_ANALYSIS), faz polling curto.
            if (!paid && !declined && paymentStatus != AsaasPaymentStatus.AwaitingRiskAnalysis)
            {
                for (var attempt = 1; attempt <= 6; attempt++)
                {
                    await Task.Delay(400);
                    try
                    {
                        var refreshed = await GetOrderAsync(payment.Id);
                        var refreshedStatus = (refreshed.Status ?? "").Trim().ToUpperInvariant();
                        if (IsAsaasCreditCardPaid(refreshedStatus) || IsAsaasCreditCardDeclined(refreshedStatus)
                            || refreshedStatus == AsaasPaymentStatus.AwaitingRiskAnalysis)
                        {
                            paymentStatus = refreshedStatus;
                            paidAt = refreshed.PaidAt ?? paidAt;
                            declined = IsAsaasCreditCardDeclined(paymentStatus);
                            paid = !declined && IsAsaasCreditCardPaid(paymentStatus);
                            chargeStatus = declined ? "DECLINED" : paid ? "PAID" : paymentStatus;
                            _logger.LogInformation(
                                "payWithCreditCard reconsulta: paymentId={PaymentId}, asaasStatus={AsaasStatus}, mappedStatus={MappedStatus}, attempt={Attempt}.",
                                payment.Id, paymentStatus, chargeStatus, attempt);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Falha ao reconsultar cobrança {PaymentId} após payWithCreditCard.", payment.Id);
                    }
                }
            }

            return MapCreditCardPaymentOrder(
                payment.Id,
                chargeStatus,
                paid ? paidAt ?? DateTime.UtcNow : paidAt,
                payment.ExternalReference ?? payId,
                AsaasDateParser.TryParse(payment.DateCreated) ?? DateTime.UtcNow,
                payment.Value);
        }

        private static OrderResponse MapCreditCardPaymentOrder(
            string paymentId,
            string chargeStatus,
            DateTime? paidAt,
            string? referenceId,
            DateTime createdAt,
            decimal? value = null)
        {
            var amountCents = value.HasValue ? (int)(value.Value * 100) : 0;
            return new OrderResponse
            {
                Id = paymentId,
                ReferenceId = referenceId ?? paymentId,
                Status = chargeStatus,
                CreatedAt = createdAt,
                PaidAt = paidAt,
                Charges = new List<PagBankChargeResponse>
                {
                    new PagBankChargeResponse
                    {
                        Id = paymentId,
                        ReferenceId = referenceId ?? paymentId,
                        Status = chargeStatus,
                        CreatedAt = createdAt,
                        PaidAt = paidAt,
                        Amount = new PagBankAmount { Value = amountCents, Currency = "BRL" },
                        PaymentMethod = new PagBankPaymentMethodResponse { Type = "CREDIT_CARD" }
                    }
                }
            };
        }

        /// <inheritdoc />
        public async Task<OrderResponse?> WaitForSubscriptionPaymentOrderAsync(string asaasSubscriptionId, int maxAttempts = 20)
        {
            if (string.IsNullOrWhiteSpace(asaasSubscriptionId))
                return null;

            if (maxAttempts < 1)
                maxAttempts = 1;

            OrderResponse? lastOrder = null;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var candidate = await GetPreferredSubscriptionPaymentAsync(asaasSubscriptionId.Trim(), preferBoleto: true);
                if (candidate != null && !string.IsNullOrEmpty(candidate.Id))
                {
                    try
                    {
                        lastOrder = await GetOrderAsync(candidate.Id);
                        var hasBoletoLine = lastOrder.Charges?.Any(c =>
                            c.PaymentMethod?.Boleto != null
                            && (!string.IsNullOrEmpty(c.PaymentMethod.Boleto.FormattedBarcode)
                                || !string.IsNullOrEmpty(c.PaymentMethod.Boleto.Barcode))) == true;
                        var hasPix = lastOrder.QrCodes?.Any() == true;
                        var billing = (candidate.BillingType ?? "").Trim().ToUpperInvariant();
                        if (hasBoletoLine)
                            return lastOrder;
                        // BOLETO sem linha digitável ainda: continuar polling (Asaas demora a emitir).
                        if (billing == AsaasBillingType.Boleto && attempt < maxAttempts)
                        {
                            await Task.Delay(1000);
                            continue;
                        }
                        // PIX ainda: continuar aguardando conversão após UpdateSubscription.
                        if ((hasPix || billing == AsaasBillingType.Pix) && attempt < maxAttempts)
                        {
                            await Task.Delay(1000);
                            continue;
                        }
                        // Qualquer cobrança no último attempt (caller decide se aceita).
                        if (attempt == maxAttempts)
                            return lastOrder;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Tentativa {Attempt}/{Max}: falha ao montar OrderResponse da cobrança {PaymentId} da assinatura {SubscriptionId}.",
                            attempt, maxAttempts, candidate.Id, asaasSubscriptionId);
                    }
                }

                if (attempt < maxAttempts)
                    await Task.Delay(1000);
            }

            return lastOrder;
        }

        private static bool IsAsaasCreditCardPaid(string statusUpper) =>
            statusUpper == AsaasPaymentStatus.Confirmed
            || statusUpper == AsaasPaymentStatus.Received;

        private static bool IsAsaasCreditCardDeclined(string statusUpper) =>
            statusUpper is "DECLINED" or "FAILED" or "REFUSED"
                or AsaasPaymentStatus.Refunded
                or AsaasPaymentStatus.Refused
                or AsaasPaymentStatus.ChargebackRequested
                or AsaasPaymentStatus.ChargebackDispute
                or AsaasPaymentStatus.Deleted;

        /// <summary>
        /// Aguarda a 1ª cobrança da assinatura ( Assaas pode listar vazia logo após o POST ).
        /// Para quando encontra cobrança paga/recusada ou esgota as tentativas.
        /// Com <paramref name="acceptAnyStatus"/> true (PIX/BOLETO), retorna assim que a cobrança existir.
        /// </summary>
        private async Task<AsaasPaymentResponse?> WaitForFirstSubscriptionPaymentAsync(
            string asaasSubscriptionId,
            bool acceptAnyStatus = false,
            int maxAttempts = 6)
        {
            if (maxAttempts < 1)
                maxAttempts = 1;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var first = await GetFirstSubscriptionPaymentAsync(asaasSubscriptionId);
                if (first != null)
                {
                    if (acceptAnyStatus)
                        return first;

                    var status = (first.Status ?? "").Trim().ToUpperInvariant();
                    if (IsAsaasCreditCardPaid(status) || IsAsaasCreditCardDeclined(status)
                        || status == AsaasPaymentStatus.AwaitingRiskAnalysis
                        || attempt == maxAttempts)
                        return first;
                }
                else if (attempt == maxAttempts)
                {
                    return null;
                }

                await Task.Delay(400);
            }

            return null;
        }

        private async Task<AsaasPaymentResponse?> GetFirstSubscriptionPaymentAsync(string asaasSubscriptionId) =>
            await GetPreferredSubscriptionPaymentAsync(asaasSubscriptionId, preferBoleto: false);

        /// <summary>
        /// Lista cobranças da assinatura e escolhe a mais útil:
        /// pendente BOLETO &gt; qualquer BOLETO &gt; pendente &gt; primeira.
        /// </summary>
        private async Task<AsaasPaymentResponse?> GetPreferredSubscriptionPaymentAsync(
            string asaasSubscriptionId,
            bool preferBoleto)
        {
            try
            {
                var httpRequest = await CreateRequestAsync(
                    HttpMethod.Get,
                    $"/subscriptions/{Uri.EscapeDataString(asaasSubscriptionId)}/payments?limit=20");
                var response = await _httpClient.SendAsync(httpRequest);
                var content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Não foi possível listar cobranças da assinatura Asaas {SubscriptionId}: {StatusCode} - {Content}",
                        asaasSubscriptionId, response.StatusCode, content);
                    return null;
                }
                var list = JsonSerializer.Deserialize<AsaasSubscriptionPaymentsListResponse>(content, JsonOptions);
                var items = list?.Data?
                    .Where(p => p != null && !string.IsNullOrEmpty(p.Id) && p.Deleted != true)
                    .ToList();
                if (items == null || items.Count == 0)
                    return null;

                static bool IsPendingStatus(string? status)
                {
                    var s = (status ?? "").Trim().ToUpperInvariant();
                    return s is "PENDING" or "OVERDUE" or "AWAITING_PAYMENT"
                        or AsaasPaymentStatus.Pending or AsaasPaymentStatus.Overdue;
                }

                if (preferBoleto)
                {
                    var pendingBoleto = items.FirstOrDefault(p =>
                        string.Equals((p.BillingType ?? "").Trim(), AsaasBillingType.Boleto, StringComparison.OrdinalIgnoreCase)
                        && IsPendingStatus(p.Status));
                    if (pendingBoleto != null)
                        return pendingBoleto;

                    var anyBoleto = items.FirstOrDefault(p =>
                        string.Equals((p.BillingType ?? "").Trim(), AsaasBillingType.Boleto, StringComparison.OrdinalIgnoreCase));
                    if (anyBoleto != null)
                        return anyBoleto;
                }

                var pending = items.FirstOrDefault(p => IsPendingStatus(p.Status));
                return pending ?? items[0];
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exceção ao buscar cobranças da assinatura Asaas {SubscriptionId}", asaasSubscriptionId);
                return null;
            }
        }

        private async Task<string> CreateOrGetAsaasCustomerAsync(
            string clientName,
            string clientEmail,
            string cpfCnpj,
            string? clientPhone,
            PagBankAddress? clientAddress,
            string? externalReference)
        {
            if (!string.IsNullOrWhiteSpace(externalReference))
            {
                var existingId = await FindCustomerIdByExternalReferenceAsync(externalReference);
                if (!string.IsNullOrEmpty(existingId))
                {
                    _logger.LogInformation("Asaas cliente encontrado por externalReference: {CustomerId}", existingId);
                    return existingId;
                }
            }
            var phone = NormalizePhone(clientPhone);
            var customerPayload = new AsaasCustomerRequest
            {
                Name = clientName,
                CpfCnpj = cpfCnpj,
                Email = clientEmail,
                Phone = phone,
                MobilePhone = phone,
                Address = NullIfBlankOrPlaceholder(clientAddress?.Street),
                AddressNumber = NullIfBlankOrPlaceholder(clientAddress?.Number, treatSnAsPlaceholder: false),
                Complement = NullIfBlankOrPlaceholder(clientAddress?.Complement),
                Province = NullIfBlankOrPlaceholder(clientAddress?.Locality),
                PostalCode = NormalizePostalCode(clientAddress?.PostalCode),
                City = TryGetCityAsIbgeCode(clientAddress?.City)?.ToString() ?? NullIfBlankOrPlaceholder(clientAddress?.City),
                State = NullIfBlankOrPlaceholder(clientAddress?.RegionCode),
                ExternalReference = externalReference
            };
            var customerReq = await CreateRequestAsync(HttpMethod.Post, "/customers");
            customerReq.Content = new StringContent(JsonSerializer.Serialize(customerPayload, JsonOptions), Encoding.UTF8, "application/json");
            var customerResponse = await _httpClient.SendAsync(customerReq);
            var customerContent = await customerResponse.Content.ReadAsStringAsync();
            if (customerResponse.IsSuccessStatusCode)
            {
                var customer = JsonSerializer.Deserialize<AsaasCustomerResponse>(customerContent, JsonOptions);
                var id = customer?.Id;
                if (!string.IsNullOrEmpty(id))
                {
                    _logger.LogInformation("Asaas cliente criado: {CustomerId}", id);
                    return id;
                }
            }
            if (customerResponse.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var listReq = await CreateRequestAsync(HttpMethod.Get, $"/customers?cpfCnpj={Uri.EscapeDataString(cpfCnpj)}");
                var listResponse = await _httpClient.SendAsync(listReq);
                var listContent = await listResponse.Content.ReadAsStringAsync();
                if (listResponse.IsSuccessStatusCode)
                {
                    var list = JsonSerializer.Deserialize<AsaasCustomerListResponse>(listContent, JsonOptions);
                    var first = list?.Data?.FirstOrDefault();
                    if (!string.IsNullOrEmpty(first?.Id))
                    {
                        _logger.LogInformation("Asaas cliente já existente, reutilizando: {CustomerId}", first.Id);
                        return first.Id;
                    }
                }
                throw new InvalidOperationException(ParseAsaasError(customerContent));
            }
            _logger.LogError("Erro ao criar/buscar cliente Asaas: {StatusCode} - {Content}", customerResponse.StatusCode, customerContent);
            throw new HttpRequestException($"Erro ao criar cliente Asaas: {customerResponse.StatusCode}. {ParseAsaasError(customerContent)}");
        }
        public async Task<OrderResponse> GetOrderAsync(string orderId)
        {
            var httpRequest = await CreateRequestAsync(HttpMethod.Get, $"/payments/{orderId}");
            var response = await _httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Erro ao consultar cobrança Asaas: {StatusCode} - {Content}", response.StatusCode, content);
                throw new HttpRequestException($"Erro ao consultar cobrança Asaas: {response.StatusCode}");
            }
            var payment = JsonSerializer.Deserialize<AsaasPaymentResponse>(content, JsonOptions);
            if (payment == null)
                throw new InvalidOperationException("Resposta inválida da API Asaas");

            var orderResponse = new OrderResponse
            {
                Id = payment.Id,
                ReferenceId = payment.ExternalReference ?? orderId,
                Status = payment.Status,
                CreatedAt = AsaasDateParser.TryParse(payment.DateCreated) ?? DateTime.UtcNow,
                PaidAt = AsaasDateParser.TryParse(payment.ConfirmedDate ?? payment.ClientPaymentDate)
            };

            var billingType = (payment.BillingType ?? "").Trim().ToUpperInvariant();
            if (billingType == AsaasBillingType.Pix)
            {
                var pixQr = await GetPixQrCodeAsync(payment.Id);
                if (pixQr != null)
                {
                    orderResponse.QrCodes = new List<PagBankQrCodeResponse>
                    {
                        new PagBankQrCodeResponse
                        {
                            Id = payment.Id,
                            Text = pixQr.Payload ?? pixQr.EncodedImage,
                            ExpirationDate = AsaasDateParser.TryParse(pixQr.ExpirationDate),
                            Amount = new PagBankAmount { Value = (int)(payment.Value * 100), Currency = "BRL" }
                        }
                    };
                }
            }
            else if (billingType == AsaasBillingType.Boleto)
            {
                var identification = await GetIdentificationFieldAsync(payment.Id);
                orderResponse.Charges = new List<PagBankChargeResponse>
                {
                    new PagBankChargeResponse
                    {
                        Id = payment.Id,
                        ReferenceId = orderResponse.ReferenceId,
                        Status = payment.Status,
                        CreatedAt = orderResponse.CreatedAt,
                        PaidAt = orderResponse.PaidAt,
                        Amount = new PagBankAmount { Value = (int)(payment.Value * 100), Currency = "BRL" },
                        PaymentMethod = new PagBankPaymentMethodResponse
                        {
                            Type = "BOLETO",
                            Boleto = new PagBankBoletoResponse
                            {
                                Id = payment.Id,
                                FormattedBarcode = identification?.IdentificationField,
                                Barcode = !string.IsNullOrEmpty(identification?.BarCode)
                                    ? identification.BarCode
                                    : identification?.IdentificationField,
                                DueDate = AsaasDateParser.TryParse(payment.DueDate),
                                Links = !string.IsNullOrEmpty(payment.BankSlipUrl)
                                    ? new List<PagBankLink> { new PagBankLink { Rel = "bankSlip", Href = payment.BankSlipUrl } }
                                    : null
                            }
                        }
                    }
                };
            }

            return orderResponse;
        }
        public async Task<OrderResponse> CancelChargeAsync(string chargeId, decimal? amount = null)
        {
            var httpRequest = await CreateRequestAsync(HttpMethod.Delete, $"/payments/{chargeId}");
            var response = await _httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Erro ao cancelar cobrança Asaas: {StatusCode} - {Content}", response.StatusCode, content);
                throw new HttpRequestException($"Erro ao cancelar cobrança Asaas: {response.StatusCode}");
            }
            return new OrderResponse { Id = chargeId, Status = "CANCELLED" };
        }

        public async Task CancelPixAutomaticAuthorizationAsync(string authorizationId)
        {
            if (string.IsNullOrWhiteSpace(authorizationId))
                throw new ArgumentException("Id da autorização PIX Automático é obrigatório.", nameof(authorizationId));

            var httpRequest = await CreateRequestAsync(HttpMethod.Delete, $"/pix/automatic/authorizations/{authorizationId}");
            var response = await _httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Erro ao cancelar autorização PIX Automático Asaas {AuthorizationId}: {StatusCode} - {Content}",
                    authorizationId, response.StatusCode, content);
                throw new HttpRequestException($"Erro ao cancelar autorização PIX Automático Asaas: {response.StatusCode}");
            }
            _logger.LogInformation("Autorização PIX Automático Asaas cancelada: {AuthorizationId}", authorizationId);
        }

        public async Task CancelPixAutomaticAuthorizationsForContractAsync(
            string asaasCustomerId,
            string baseContractId,
            string? exceptAuthorizationId = null)
        {
            if (string.IsNullOrWhiteSpace(asaasCustomerId) || string.IsNullOrWhiteSpace(baseContractId))
                return;

            var exceptId = exceptAuthorizationId?.Trim();
            var offset = 0;
            const int limit = 100;
            while (true)
            {
                var path =
                    $"/pix/automatic/authorizations?customerId={Uri.EscapeDataString(asaasCustomerId.Trim())}&offset={offset}&limit={limit}";
                var httpRequest = await CreateRequestAsync(HttpMethod.Get, path);
                var response = await _httpClient.SendAsync(httpRequest);
                var content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Erro ao listar autorizações PIX Automático (customerId={CustomerId}, contractId={ContractId}): {StatusCode} - {Content}",
                        asaasCustomerId, baseContractId, response.StatusCode, content);
                    return;
                }

                var list = JsonSerializer.Deserialize<AsaasPixAutomaticAuthorizationListResponse>(content, JsonOptions);
                var items = list?.Data ?? new List<AsaasPixAutomaticAuthorizationListItem>();
                foreach (var item in items)
                {
                    var authId = item.Id?.Trim();
                    if (string.IsNullOrEmpty(authId)
                        || string.Equals(authId, exceptId, StringComparison.OrdinalIgnoreCase)
                        || !PixAutomaticContractIdMatches(item.ContractId, baseContractId))
                        continue;

                    if (string.Equals(item.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        await CancelPixAutomaticAuthorizationAsync(authId);
                        _logger.LogInformation(
                            "Autorização PIX Automático stale cancelada: {AuthorizationId} (contractId={ContractId}, status={Status})",
                            authId, item.ContractId, item.Status);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Falha ao cancelar autorização PIX Automático stale {AuthorizationId} (contractId={ContractId}).",
                            authId, item.ContractId);
                    }
                }

                if (list?.HasMore != true || items.Count == 0)
                    break;
                offset += limit;
            }
        }

        private static bool PixAutomaticContractIdMatches(string? authContractId, string baseContractId)
        {
            if (string.IsNullOrWhiteSpace(authContractId))
                return false;
            var normalized = authContractId.Trim();
            return string.Equals(normalized, baseContractId, StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith(baseContractId + "-", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Cria autorização PIX Automático (MONTHLY ou ANNUALLY). Retorna id da autorização, payload do QR Code e data de expiração do primeiro pagamento.</summary>
        public async Task<AsaasPixAutomaticAuthorizationResponse?> CreatePixAutomaticAuthorizationAsync(
            string asaasCustomerId,
            string contractId,
            decimal value,
            string description,
            string frequency = "ANNUALLY",
            int immediateExpirationSeconds = 86400)
        {
            var normalizedFrequency = (frequency ?? "ANNUALLY").Trim().ToUpperInvariant();
            if (normalizedFrequency is not ("MONTHLY" or "ANNUALLY"))
                throw new ArgumentException("Frequência PIX Automático inválida. Use MONTHLY ou ANNUALLY.", nameof(frequency));

            var baseUrl = await GetBaseUrlAsync();
            var apiKey = await GetApiKeyAsync();
            var keyMask = apiKey.Length >= 4 ? "..." + apiKey.Substring(apiKey.Length - 4) : "(não exibido)";
            _logger.LogInformation(
                "PIX Automático: enviando para conta Asaas baseUrl={BaseUrl}, apiKey termina com {KeyMask}, contractId={ContractId}, frequency={Frequency}",
                baseUrl, keyMask, contractId, normalizedFrequency);

            var startDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
            var periodLabel = normalizedFrequency == "MONTHLY" ? "mensal" : "anual";
            var request = new AsaasPixAutomaticAuthorizationRequest
            {
                Frequency = normalizedFrequency,
                ContractId = contractId,
                StartDate = startDate,
                CustomerId = asaasCustomerId,
                Value = value,
                Description = description.Length > 35 ? description.Substring(0, 35) : description,
                ImmediateQrCode = new AsaasPixAutomaticImmediateQrCodeRequest
                {
                    OriginalValue = value,
                    ExpirationSeconds = immediateExpirationSeconds,
                    Description = $"Primeira cobrança {periodLabel} - PIX Automático"
                }
            };
            var httpRequest = await CreateRequestAsync(HttpMethod.Post, "/pix/automatic/authorizations");
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json");
            var response = await _httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Erro ao criar autorização PIX Automático: {StatusCode} - {Content}", response.StatusCode, content);
                var msg = content.Length > 300 ? content.Substring(0, 300) + "..." : content;
                throw new InvalidOperationException(
                    $"Asaas PIX Automático: a API retornou {(int)response.StatusCode} ({response.StatusCode}). Detalhe: {msg}");
            }
            return JsonSerializer.Deserialize<AsaasPixAutomaticAuthorizationResponse>(content, JsonOptions);
        }

        /// <summary>Cria cobrança PIX vinculada à autorização PIX Automático (para recorrência). A cobrança será debitada automaticamente na data de vencimento.</summary>
        public async Task<OrderResponse?> CreatePaymentWithPixAutomaticAuthorizationAsync(
            string asaasCustomerId,
            string pixAutomaticAuthorizationId,
            decimal value,
            DateTime dueDate,
            string externalReference,
            string description)
        {
            var request = new AsaasPaymentRequest
            {
                Customer = asaasCustomerId,
                BillingType = "PIX",
                Value = value,
                DueDate = dueDate.ToString("yyyy-MM-dd"),
                ExternalReference = externalReference,
                Description = description.Length > 500 ? description.Substring(0, 500) : description,
                PixAutomaticAuthorizationId = pixAutomaticAuthorizationId
            };
            var httpRequest = await CreateRequestAsync(HttpMethod.Post, "/payments");
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json");
            var response = await _httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Erro ao criar cobrança PIX Automático: {StatusCode} - {Content}", response.StatusCode, content);
                return null;
            }
            var payment = JsonSerializer.Deserialize<AsaasPaymentResponse>(content, JsonOptions);
            if (payment == null) return null;
            return new OrderResponse
            {
                Id = payment.Id,
                ReferenceId = externalReference,
                Status = payment.Status,
                CreatedAt = AsaasDateParser.TryParse(payment.DateCreated) ?? DateTime.UtcNow,
                PaidAt = AsaasDateParser.TryParse(payment.ConfirmedDate ?? payment.ClientPaymentDate)
            };
        }

        public async Task<bool> ValidateWebhookSignatureAsync(string payload, string signature)
        {
            var token = await _systemConfig.GetValueAsync(AsaasConfigKey.WebhookToken.ToConfigKey());
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("{Key} não configurado. Validação de webhook ignorada.", AsaasConfigKey.WebhookToken.ToConfigKey());
                return true;
            }
            if (string.IsNullOrEmpty(signature))
            {
                _logger.LogWarning("Webhook Asaas sem header asaas-access-token.");
                return false;
            }
            return string.Equals(signature.Trim(), token.Trim(), StringComparison.Ordinal);
        }
        public async Task<PixTransferResult> CreatePixTransferAsync(string referenceId, decimal amount, string pixKey, string pixKeyType, string receiverName, string? description = null)
        {
            var body = new
            {
                value = amount,
                pixAddressKey = pixKey,
                pixAddressKeyType = pixKeyType,
                description = description ?? $"Repasse - Ref: {referenceId}"
            };
            var httpRequest = await CreateRequestAsync(HttpMethod.Post, "/transfers");
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
            var response = await _httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Erro na transferência PIX Asaas: {StatusCode} - {Content}", response.StatusCode, content);
                return new PixTransferResult { Success = false, ErrorMessage = content };
            }
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;
            return new PixTransferResult
            {
                Success = true,
                TransferId = id,
                Status = status,
                Amount = amount,
                CreatedAt = DateTime.UtcNow
            };
        }
        public async Task<PixTransferResult> GetPixTransferAsync(string transferId)
        {
            var httpRequest = await CreateRequestAsync(HttpMethod.Get, $"/transfers/{transferId}");
            var response = await _httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Erro ao consultar transferência PIX Asaas: {StatusCode}", response.StatusCode);
                return new PixTransferResult { Success = false, TransferId = transferId, ErrorMessage = content };
            }
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;
            var value = 0m;
            if (root.TryGetProperty("value", out var v))
                value = v.GetDecimal();
            return new PixTransferResult
            {
                Success = true,
                TransferId = transferId,
                Status = status,
                Amount = value
            };
        }
        public async Task<AsaasPixQrCodeResponse?> GetPixQrCodeAsync(string asaasPaymentId)
        {
            try
            {
                var httpRequest = await CreateRequestAsync(HttpMethod.Get, $"/payments/{asaasPaymentId}/pixQrCode");
                var response = await _httpClient.SendAsync(httpRequest);
                var content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Falha ao obter QR Code PIX: {StatusCode} - {Content}", response.StatusCode, content);
                    return null;
                }
                return JsonSerializer.Deserialize<AsaasPixQrCodeResponse>(content, JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exceção ao obter QR Code PIX para pagamento Asaas {AsaasPaymentId}", asaasPaymentId);
                return null;
            }
        }
        private async Task<AsaasIdentificationFieldResponse?> GetIdentificationFieldAsync(string paymentId)
        {
            try
            {
                var httpRequest = await CreateRequestAsync(HttpMethod.Get, $"/payments/{paymentId}/identificationField");
                var response = await _httpClient.SendAsync(httpRequest);
                var content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Falha ao obter linha digitável do boleto: {StatusCode} - {Content}", response.StatusCode, content);
                    return null;
                }
                return JsonSerializer.Deserialize<AsaasIdentificationFieldResponse>(content, JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exceção ao obter linha digitável para pagamento {PaymentId}", paymentId);
                return null;
            }
        }
        private static AsaasCreditCard MapCard(PagBankCard card)
        {
            var expiry = (card.ExpirationDate ?? "").Trim();
            string month = "", year = "";
            if (expiry.Length >= 7)
            {
                var parts = expiry.Split('-');
                if (parts.Length >= 2)
                {
                    month = parts[1].Trim();
                    year = parts[0].Trim().Length == 4 ? parts[0].Trim().Substring(2, 2) : parts[0].Trim();
                }
            }
            return new AsaasCreditCard
            {
                HolderName = card.Holder?.Name ?? "",
                Number = (card.Number ?? "").Replace(" ", ""),
                ExpiryMonth = month,
                ExpiryYear = year,
                Ccv = card.SecurityCode ?? ""
            };
        }
        private async Task<string?> FindCustomerIdByExternalReferenceAsync(string externalReference, CancellationToken cancellationToken = default)
        {
            var listByRefReq = await CreateRequestAsync(HttpMethod.Get, $"/customers?externalReference={Uri.EscapeDataString(externalReference.Trim())}");
            var listByRefRes = await _httpClient.SendAsync(listByRefReq, cancellationToken);
            if (!listByRefRes.IsSuccessStatusCode)
                return null;
            var listByRefContent = await listByRefRes.Content.ReadAsStringAsync(cancellationToken);
            var listByRef = JsonSerializer.Deserialize<AsaasCustomerListResponse>(listByRefContent, JsonOptions);
            var id = listByRef?.Data?.FirstOrDefault()?.Id;
            return string.IsNullOrEmpty(id) ? null : id;
        }

        private static string? NullIfBlankOrPlaceholder(string? value, bool treatSnAsPlaceholder = true)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            var trimmed = value.Trim();
            if (string.Equals(trimmed, DefaultDisplayStrings.NaoInformado, StringComparison.OrdinalIgnoreCase))
                return null;
            if (treatSnAsPlaceholder && string.Equals(trimmed, "S/N", StringComparison.OrdinalIgnoreCase))
                return null;
            if (trimmed == "00000000" || trimmed == "00000-000")
                return null;
            return trimmed;
        }

        private static string? NormalizePostalCodeForUpdate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            var digits = new string(value.Where(char.IsDigit).ToArray());
            if (digits.Length != 8 || digits.All(c => c == '0'))
                return null;
            return digits;
        }

        private static string NormalizeCpfCnpj(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            return new string(value.Where(char.IsDigit).ToArray());
        }
        private static bool IsValidCpf(string cpf)
        {
            if (cpf.Length != 11 || cpf.All(c => c == cpf[0])) return false;
            int[] mult1 = { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
            int[] mult2 = { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };
            int sum = 0;
            for (int i = 0; i < 9; i++) sum += (cpf[i] - '0') * mult1[i];
            int d1 = sum % 11; d1 = d1 < 2 ? 0 : 11 - d1;
            if ((cpf[9] - '0') != d1) return false;
            sum = 0;
            for (int i = 0; i < 10; i++) sum += (cpf[i] - '0') * mult2[i];
            int d2 = sum % 11; d2 = d2 < 2 ? 0 : 11 - d2;
            return (cpf[10] - '0') == d2;
        }
        private static bool IsValidCnpj(string cnpj)
        {
            if (cnpj.Length != 14 || cnpj.All(c => c == cnpj[0])) return false;
            int[] mult1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int[] mult2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int sum = 0;
            for (int i = 0; i < 12; i++) sum += (cnpj[i] - '0') * mult1[i];
            int d1 = sum % 11; d1 = d1 < 2 ? 0 : 11 - d1;
            if ((cnpj[12] - '0') != d1) return false;
            sum = 0;
            for (int i = 0; i < 13; i++) sum += (cnpj[i] - '0') * mult2[i];
            int d2 = sum % 11; d2 = d2 < 2 ? 0 : 11 - d2;
            return (cnpj[13] - '0') == d2;
        }
        private static string? NormalizePhone(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var digits = new string(value.Where(char.IsDigit).ToArray());
            return digits.Length >= 10 ? digits : null;
        }
        private static int? TryGetCityAsIbgeCode(string? city)
        {
            if (string.IsNullOrWhiteSpace(city)) return null;
            var digits = new string(city.Where(char.IsDigit).ToArray());
            if (digits.Length == 7 && int.TryParse(digits, out var code)) return code;
            return null;
        }
        private static string? NormalizePostalCode(string? value) =>
            NormalizePostalCodeForUpdate(value);
        private static string ParseAsaasError(string content)
        {
            try
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                if (root.TryGetProperty("errors", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    var messages = new List<string>();
                    foreach (var item in arr.EnumerateArray())
                    {
                        var desc = item.TryGetProperty("description", out var d) ? d.GetString() : null;
                        if (!string.IsNullOrEmpty(desc)) messages.Add(desc);
                    }
                    if (messages.Count > 0) return string.Join(" ", messages);
                }
            }
            catch { }
            return "Dados inválidos. Verifique os dados em 'Meu Perfil' (CPF/CNPJ, Nome, Email).";
        }

        private static string ParseAsaasCreditCardError(string content)
        {
            try
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                if (root.TryGetProperty("errors", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    string? firstCode = null;
                    var descriptions = new List<string>();
                    foreach (var item in arr.EnumerateArray())
                    {
                        if (firstCode == null && item.TryGetProperty("code", out var codeEl))
                            firstCode = codeEl.GetString();
                        var desc = item.TryGetProperty("description", out var d) ? d.GetString() : null;
                        if (!string.IsNullOrEmpty(desc)) descriptions.Add(desc);
                    }
                    var raw = descriptions.Count > 0 ? string.Join(" ", descriptions) : null;
                    return AsaasCreditCardErrorMapper.ToUserFriendlyMessage(raw, firstCode);
                }
            }
            catch { }
            return AsaasCreditCardErrorMapper.ToUserFriendlyMessage(null, null);
        }
    }
}