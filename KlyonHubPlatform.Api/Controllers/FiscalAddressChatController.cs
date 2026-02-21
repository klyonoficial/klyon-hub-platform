using Microsoft.AspNetCore.Mvc;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.Services;

namespace KlyonHubPlatform.Api.Controllers
{
    /// <summary>
    /// API do chatbot de endereço fiscal (Klyon Coworking - Shopping Moxuara).
    /// Responde apenas dúvidas sobre o serviço de endereço fiscal.
    /// </summary>
    [ApiController]
    [Route("api/fiscal-address-chat")]
    public class FiscalAddressChatController : ControllerBase
    {
        private readonly IFiscalAddressChatService _chatService;

        public FiscalAddressChatController(IFiscalAddressChatService chatService)
        {
            _chatService = chatService;
        }

        /// <summary>
        /// Retorna a mensagem de boas-vindas do chat (convite para o cliente enviar a primeira pergunta).
        /// </summary>
        [HttpGet("initial")]
        [ProducesResponseType(typeof(FiscalAddressChatInitialResponse), StatusCodes.Status200OK)]
        public IActionResult GetInitialMessage()
        {
            var message = _chatService.GetInitialMessage();
            return Ok(new FiscalAddressChatInitialResponse { Message = message });
        }

        /// <summary>
        /// Envia uma mensagem do usuário e retorna a resposta do assistente.
        /// </summary>
        /// <param name="request">Objeto com a propriedade "message" (pergunta do usuário).</param>
        /// <param name="cancellationToken">Token de cancelamento.</param>
        [HttpPost("chat")]
        [ProducesResponseType(typeof(FiscalAddressChatReplyResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Chat([FromBody] FiscalAddressChatRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(new { error = "A mensagem não pode ser vazia." });

            var reply = await _chatService.GetReplyAsync(request.Message.Trim(), cancellationToken);
            return Ok(new FiscalAddressChatReplyResponse { Reply = reply });
        }
    }

    /// <summary>Resposta da mensagem inicial do chat.</summary>
    public class FiscalAddressChatInitialResponse
    {
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Request do endpoint de chat (mensagem do usuário).</summary>
    public class FiscalAddressChatRequest
    {
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Resposta do endpoint de chat (resposta do assistente).</summary>
    public class FiscalAddressChatReplyResponse
    {
        public string Reply { get; set; } = string.Empty;
    }
}
