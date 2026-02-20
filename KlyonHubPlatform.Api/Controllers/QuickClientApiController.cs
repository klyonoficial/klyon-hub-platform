using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KlyonHubPlatform.Application.DTOs;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.Services;
using static KlyonHubPlatform.Domain.Core.Constants.RoleNames;
using System.Security.Claims;
using KlyonHubPlatform.Api.Constants;
using KlyonHubPlatform.Api.Requests;
using KlyonHubPlatform.Api.Responses;
using KlyonHubPlatform.Api.Dtos;

namespace KlyonHubPlatform.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = Administrator)]
    public class QuickClientApiController : ControllerBase
    {
        private readonly IQuickClientRegistrationService _quickClientRegistrationService;
        private readonly ICoworkingService _coworkingService;
        private readonly IMapper _mapper;

        public QuickClientApiController(
            IQuickClientRegistrationService quickClientRegistrationService,
            ICoworkingService coworkingService,
            IMapper mapper)
        {
            _quickClientRegistrationService = quickClientRegistrationService;
            _coworkingService = coworkingService;
            _mapper = mapper;
        }

        [HttpPost("Register")]
        public async Task<IActionResult> RegisterClient([FromBody] QuickClientRegistrationRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ContactEmail) || string.IsNullOrWhiteSpace(request.ContactName))
                {
                    return BadRequest(new QuickClientRegistrationResponse
                    {
                        Success = false,
                        Message = ErrorJson.ContactEmailAndNameRequired
                    });
                }
                if (request.MonthlyValue <= 0)
                {
                    return BadRequest(new QuickClientRegistrationResponse
                    {
                        Success = false,
                        Message = ErrorJson.AnnualValueMustBeGreaterThanZero
                    });
                }
                if (request.CoworkingAddressId <= 0)
                {
                    return BadRequest(new QuickClientRegistrationResponse
                    {
                        Success = false,
                        Message = ErrorJson.FiscalAddressRequired
                    });
                }

                var input = new QuickClientRegistrationInput
                {
                    ContactEmail = request.ContactEmail,
                    ContactName = request.ContactName,
                    RazaoSocial = request.RazaoSocial,
                    NomeFantasia = request.NomeFantasia,
                    Cnpj = request.Cnpj,
                    Cpf = request.Cpf,
                    ContactPhone = request.ContactPhone,
                    Address = request.Address,
                    City = request.City,
                    State = request.State,
                    ZipCode = request.ZipCode,
                    CoworkingAddressId = request.CoworkingAddressId,
                    MonthlyValue = request.MonthlyValue,
                    PaymentMethod = request.PaymentMethod ?? "PIX"
                };

                var adminUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : (int?)null;
                var result = await _quickClientRegistrationService.RegisterAsync(
                    input,
                    adminUserId,
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Request.Headers.UserAgent.FirstOrDefault(),
                    cancellationToken);

                if (!result.Success)
                {
                    var message = GetFailureMessage(result.FailureKey!, result.FailureArgument);
                    return BadRequest(new QuickClientRegistrationResponse { Success = false, Message = message });
                }

                var messageOk = $"Cliente cadastrado e contrato criado com sucesso! Contrato #{result.ContractNumber}";
                if (!string.IsNullOrEmpty(result.WarningKey))
                    messageOk += $" {result.WarningArgument}";

                return Ok(new QuickClientRegistrationResponse
                {
                    Success = true,
                    Message = messageOk,
                    ContractNumber = result.ContractNumber,
                    PixCode = result.PixCode,
                    PaymentId = result.PaymentId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new QuickClientRegistrationResponse
                {
                    Success = false,
                    Message = $"Erro ao cadastrar cliente: {ex.Message}"
                });
            }
        }

        private static string GetFailureMessage(string failureKey, string? failureArgument)
        {
            return failureKey switch
            {
                QuickClientRegistrationService.FailureKeyEmailAndContactNameRequired => ErrorJson.ContactEmailAndNameRequired,
                QuickClientRegistrationService.FailureKeyAnnualValueMustBeGreaterThanZero => ErrorJson.AnnualValueMustBeGreaterThanZero,
                QuickClientRegistrationService.FailureKeyAddressNotFound => ErrorJson.AddressNotFoundQuickClient,
                QuickClientRegistrationService.FailureKeyPartnerSuspendedNoNewContracts => ErrorJson.PartnerSuspendedNoNewContracts,
                QuickClientRegistrationService.FailureKeyAddressNotActiveForSales => ErrorJson.AddressNotActiveForSales,
                QuickClientRegistrationService.FailureKeyAddressNotAvailableForContractSelected => ErrorJson.AddressNotAvailableForContract,
                QuickClientRegistrationService.FailureKeyClientAlreadyExistsWithEmail => string.Format(ErrorJson.ClientAlreadyExistsWithEmail, failureArgument ?? ""),
                QuickClientRegistrationService.FailureKeyCnpjAlreadyRegistered => string.Format(ErrorJson.CnpjAlreadyRegistered, failureArgument ?? ""),
                QuickClientRegistrationService.FailureKeyCpfAlreadyRegistered => string.Format(ErrorJson.CpfAlreadyRegistered, failureArgument ?? ""),
                _ => failureArgument ?? "Erro ao processar solicitação."
            };
        }

        [HttpGet("AvailableAddresses")]
        public async Task<IActionResult> GetAvailableAddresses(CancellationToken cancellationToken = default)
        {
            try
            {
                var addressesWithLocation = await _coworkingService.GetAvailableAddressesAsync(null, cancellationToken);
                var dtos = _mapper.Map<List<AvailableAddressDto>>(addressesWithLocation);
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ErrorJson.ErrorFetchingAddresses, details = ex.Message });
            }
        }
    }
}
