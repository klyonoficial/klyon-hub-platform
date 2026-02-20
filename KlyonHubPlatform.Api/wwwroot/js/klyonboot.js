class KlyonBoot {
    constructor(options = {}) {
        this.apiBaseUrl = options.apiBaseUrl || '/api/chatbot';
        this.sessionId = this.generateSessionId();
        this.conversationId = null;
        this.isOpen = false;
        this.userId = options.userId || null;
        this.init();
    }
    generateSessionId() {
        return 'session_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);
    }
    init() {
        this.createChatWidget();
        this.attachEventListeners();
        this.startConversation();
    }
    createChatWidget() {
        const widgetHTML = `
            <div id="klyonboot-widget" class="klyonboot-widget">
                <!-- Botão flutuante -->
                <button id="klyonboot-toggle" class="klyonboot-toggle" aria-label="Abrir chat">
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M20 2H4C2.9 2 2 2.9 2 4V22L6 18H20C21.1 18 22 17.1 22 16V4C22 2.9 21.1 2 20 2ZM20 16H6L4 18V4H20V16Z" fill="currentColor"/>
                        <path d="M7 9H17V11H7V9ZM7 12H14V14H7V12Z" fill="currentColor"/>
                    </svg>
                    <span class="klyonboot-badge" id="klyonboot-badge">1</span>
                </button>
                <!-- Janela do chat -->
                <div id="klyonboot-window" class="klyonboot-window" style="display: none;">
                    <!-- Cabeçalho -->
                    <div class="klyonboot-header">
                        <div class="klyonboot-header-info">
                            <div class="klyonboot-avatar">
                                <svg width="32" height="32" viewBox="0 0 32 32" fill="none">
                                    <circle cx="16" cy="16" r="16" fill="#6C5CE7"/>
                                    <text x="16" y="22" text-anchor="middle" fill="white" font-size="18" font-weight="bold">K</text>
                                </svg>
                            </div>
                            <div>
                                <div class="klyonboot-title">KlyonBoot</div>
                                <div class="klyonboot-subtitle">Assistente Oficial</div>
                            </div>
                        </div>
                        <button id="klyonboot-close" class="klyonboot-close-btn" aria-label="Fechar chat">
                            <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
                                <path d="M15 5L5 15M5 5L15 15" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
                            </svg>
                        </button>
                    </div>
                    <!-- Área de mensagens -->
                    <div id="klyonboot-messages" class="klyonboot-messages"></div>
                    <!-- Feedback de resolução -->
                    <div id="klyonboot-feedback" class="klyonboot-feedback" style="display: none;">
                        <p>Consegui ajudar você?</p>
                        <div class="klyonboot-feedback-buttons">
                            <button class="klyonboot-btn-yes" data-resolved="true">
                                👍 Sim, ajudou!
                            </button>
                            <button class="klyonboot-btn-no" data-resolved="false">
                                👎 Não resolveu
                            </button>
                        </div>
                    </div>
                    <!-- Input de mensagem -->
                    <div class="klyonboot-input-container">
                        <input 
                            type="text" 
                            id="klyonboot-input" 
                            class="klyonboot-input" 
                            placeholder="Digite sua mensagem..."
                            autocomplete="off"
                        />
                        <button id="klyonboot-send" class="klyonboot-send-btn" aria-label="Enviar mensagem">
                            <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
                                <path d="M2 10L18 2L10 18L8 11L2 10Z" fill="currentColor"/>
                            </svg>
                        </button>
                    </div>
                    <!-- Powered by -->
                    <div class="klyonboot-footer">
                        Powered by <strong>Klyon Coworking</strong>
                    </div>
                </div>
            </div>
        `;
        document.body.insertAdjacentHTML('beforeend', widgetHTML);
    }
    attachEventListeners() {
        document.getElementById('klyonboot-toggle').addEventListener('click', () => {
            this.toggleChat();
        });
        document.getElementById('klyonboot-close').addEventListener('click', () => {
            this.closeChat();
        });
        document.getElementById('klyonboot-send').addEventListener('click', () => {
            this.sendMessage();
        });
        document.getElementById('klyonboot-input').addEventListener('keypress', (e) => {
            if (e.key === 'Enter') {
                this.sendMessage();
            }
        });
        document.addEventListener('click', (e) => {
            if (e.target.closest('.klyonboot-btn-yes, .klyonboot-btn-no')) {
                const wasResolved = e.target.closest('button').dataset.resolved === 'true';
                this.submitFeedback(wasResolved);
            }
        });
    }
    toggleChat() {
        if (this.isOpen) {
            this.closeChat();
        } else {
            this.openChat();
        }
    }
    openChat() {
        document.getElementById('klyonboot-window').style.display = 'flex';
        document.getElementById('klyonboot-toggle').style.display = 'none';
        this.isOpen = true;
        setTimeout(() => {
            document.getElementById('klyonboot-input').focus();
        }, 300);
        this.hideBadge();
    }
    closeChat() {
        document.getElementById('klyonboot-window').style.display = 'none';
        document.getElementById('klyonboot-toggle').style.display = 'flex';
        this.isOpen = false;
    }
    showBadge(count = 1) {
        const badge = document.getElementById('klyonboot-badge');
        badge.textContent = count;
        badge.style.display = 'block';
    }
    hideBadge() {
        document.getElementById('klyonboot-badge').style.display = 'none';
    }
    async startConversation() {
        try {
            const response = await fetch(`${this.apiBaseUrl}/start`, {
                method: 'POST',
                headers: { [(window.KlyonHeaders && window.KlyonHeaders.ContentType) || 'Content-Type']: (window.KlyonHeaders && window.KlyonHeaders.ContentTypeJson) || 'application/json' },
                body: JSON.stringify({
                    sessionId: this.sessionId,
                    userId: this.userId
                })
            });
            if (!response.ok) {
                let errorData;
                try {
                    errorData = await response.json();
                } catch (parseError) {
                    errorData = { 
                        error: `Erro ${response.status}: ${response.statusText}`,
                        details: 'Não foi possível processar a resposta do servidor'
                    };
                }
                if (errorData.error && (errorData.error.includes('Tabelas') || errorData.error.includes('tabelas')) && errorData.solution) {
                    const errorMsg = `${errorData.error}\n\n${errorData.solution}\n\nDetalhes: ${errorData.details || errorData.technicalDetails || ''}`;
                    throw new Error(errorMsg);
                }
                let errorMessage = errorData.error || errorData.details || `Erro ${response.status}: ${response.statusText}`;
                if (errorData.solution) {
                    errorMessage += `\n\nSolução: ${errorData.solution}`;
                }
                if (errorData.technicalDetails && errorData.technicalDetails !== errorData.error) {
                    errorMessage += `\n\nDetalhes técnicos: ${errorData.technicalDetails}`;
                }
                throw new Error(errorMessage);
            }
            const data = await response.json();
            const conversationId = data.id || data.Id;
            if (!data || !conversationId) {
                throw new Error('Resposta inválida do servidor: ID da conversa não encontrado');
            }
            this.conversationId = conversationId;
            const messages = data.messages || data.Messages;
            if (messages && Array.isArray(messages) && messages.length > 0) {
                messages.forEach(msg => {
                    if (msg) {
                        const content = msg.content || msg.Content;
                        const messageType = msg.messageType || msg.MessageType || 'bot';
                        const sentAt = msg.sentAt || msg.SentAt;
                        if (content) {
                            this.addMessage(content, messageType, sentAt);
                        }
                    }
                });
            } else {
                this.addMessage('Olá! 😊 Sou o KlyonBoot, assistente oficial da plataforma Klyon Coworking. Como posso ajudar você hoje?', 'bot');
            }
        } catch (error) {
            console.error('Erro ao iniciar conversa:', error);
            let errorMsg = error.message || 'Desculpe, ocorreu um erro ao iniciar o chat. Por favor, tente novamente.';
            if (errorMsg.includes('Tabelas') || errorMsg.includes('tabelas') || errorMsg.includes('migrations')) {
                errorMsg = '⚠️ Erro: As tabelas do KlyonBoot não foram criadas no banco de dados.\n\n' +
                          'Solução: Acesse /Admin/DatabaseManager e execute as migrations pendentes.\n\n' +
                          'Detalhes: ' + errorMsg;
            }
            this.addMessage(errorMsg, 'bot', null, true);
        }
    }
    async sendMessage() {
        const input = document.getElementById('klyonboot-input');
        const message = input.value.trim();
        if (!message) return;
        this.addMessage(message, 'user');
        input.value = '';
        this.showTypingIndicator();
        try {
            const response = await fetch(`${this.apiBaseUrl}/message`, {
                method: 'POST',
                headers: { [(window.KlyonHeaders && window.KlyonHeaders.ContentType) || 'Content-Type']: (window.KlyonHeaders && window.KlyonHeaders.ContentTypeJson) || 'application/json' },
                body: JSON.stringify({
                    sessionId: this.sessionId,
                    message: message,
                    userId: this.userId
                })
            });
            if (!response.ok) {
                const errorData = await response.json().catch(() => ({ error: 'Erro desconhecido' }));
                throw new Error(errorData.error || `Erro ${response.status}: ${response.statusText}`);
            }
            const data = await response.json();
            this.removeTypingIndicator();
            if (data && data.response) {
                this.addMessage(data.response, 'bot', data.sentAt);
                if (data.suggestHumanSupport) {
                    this.showFeedback();
                }
            } else {
                this.addMessage('Desculpe, não recebi uma resposta válida. Por favor, tente novamente.', 'bot', null, true);
            }
        } catch (error) {
            console.error('Erro ao enviar mensagem:', error);
            this.removeTypingIndicator();
            const errorMessage = error.message || 'Desculpe, ocorreu um erro. Por favor, tente novamente.';
            this.addMessage(errorMessage, 'bot', null, true);
        }
    }
    addMessage(content, type, timestamp = null, isError = false) {
        const messagesContainer = document.getElementById('klyonboot-messages');
        const messageDiv = document.createElement('div');
        let className = `klyonboot-message klyonboot-message-${type}`;
        if (isError && type === 'bot') {
            className += ' error';
        }
        messageDiv.className = className;
        const time = timestamp ? new Date(timestamp).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }) 
                                : new Date().toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
        messageDiv.innerHTML = `
            <div class="klyonboot-message-content">${this.formatMessage(content)}</div>
            <div class="klyonboot-message-time">${time}</div>
        `;
        messagesContainer.appendChild(messageDiv);
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
        if (!this.isOpen && type === 'bot') {
            this.showBadge();
        }
    }
    formatMessage(content) {
        if (!content || typeof content !== 'string') {
            return 'Mensagem inválida';
        }
        const urlRegex = /(https?:\/\/[^\s]+)/g;
        content = content.replace(urlRegex, '<a href="$1" target="_blank" rel="noopener noreferrer">$1</a>');
        content = content.replace(/\n/g, '<br>');
        return content;
    }
    showTypingIndicator() {
        const messagesContainer = document.getElementById('klyonboot-messages');
        const typingDiv = document.createElement('div');
        typingDiv.id = 'klyonboot-typing';
        typingDiv.className = 'klyonboot-message klyonboot-message-bot';
        typingDiv.innerHTML = `
            <div class="klyonboot-typing-indicator">
                <span></span><span></span><span></span>
            </div>
        `;
        messagesContainer.appendChild(typingDiv);
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }
    removeTypingIndicator() {
        const typingIndicator = document.getElementById('klyonboot-typing');
        if (typingIndicator) {
            typingIndicator.remove();
        }
    }
    showFeedback() {
        document.getElementById('klyonboot-feedback').style.display = 'block';
    }
    hideFeedback() {
        document.getElementById('klyonboot-feedback').style.display = 'none';
    }
    async submitFeedback(wasResolved) {
        try {
            await fetch(`${this.apiBaseUrl}/conversation/${this.conversationId}/resolve`, {
                method: 'POST',
                headers: { [(window.KlyonHeaders && window.KlyonHeaders.ContentType) || 'Content-Type']: (window.KlyonHeaders && window.KlyonHeaders.ContentTypeJson) || 'application/json' },
                body: JSON.stringify({ wasResolved })
            });
            this.hideFeedback();
            if (wasResolved) {
                this.addMessage('Ótimo! Fico feliz em ter ajudado! 😊', 'bot');
            } else {
                this.addMessage('Entendo. Posso te direcionar para nosso atendimento humano. Por favor, envie um e-mail para suporte@klyon.com.br ou entre em contato pelo WhatsApp. 📞', 'bot');
            }
            setTimeout(() => {
                this.endConversation();
            }, 3000);
        } catch (error) {
            console.error('Erro ao enviar feedback:', error);
        }
    }
    async endConversation() {
        try {
            await fetch(`${this.apiBaseUrl}/conversation/${this.conversationId}/end`, {
                method: 'POST'
            });
        } catch (error) {
            console.error('Erro ao encerrar conversa:', error);
        }
    }
}
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        window.klyonboot = new KlyonBoot();
    });
} else {
    window.klyonboot = new KlyonBoot();
}