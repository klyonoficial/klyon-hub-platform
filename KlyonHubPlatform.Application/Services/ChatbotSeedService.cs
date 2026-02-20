using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Interfaces;
namespace KlyonHubPlatform.Application.Services
{
    public class ChatbotSeedService
    {
        private readonly IChatbotRepository _repository;
        public ChatbotSeedService(IChatbotRepository repository)
        {
            _repository = repository;
        }
        public async Task SeedFaqDataAsync()
        {
            var faqItems = GetInitialFaqItems();
            foreach (var faqItem in faqItems)
            {
                await _repository.CreateFaqItemAsync(faqItem);
            }
        }
        private List<ChatFaqItem> GetInitialFaqItems()
        {
            return new List<ChatFaqItem>
            {
                new ChatFaqItem
                {
                    Category = "Cadastro",
                    Question = "Como me cadastrar como parceiro (coworking)?",
                    Answer = "Para se cadastrar como parceiro na Klyon:\n\n1. Acesse nossa plataforma\n2. Clique em \"Cadastrar Coworking\"\n3. Preencha seus dados: CNPJ, razão social, endereço\n4. Envie os documentos necessários\n5. Aguarde aprovação da equipe Klyon\n\nApós aprovação, você poderá cadastrar seus espaços e começar a receber reservas! 🎉",
                    Keywords = "cadastro,cadastrar,parceiro,coworking,registrar,inscrever,novo,como viro parceiro",
                    Intentions = "cadastro_parceiro",
                    DisplayOrder = 1,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },
                new ChatFaqItem
                {
                    Category = "Cadastro",
                    Question = "Como me cadastrar como cliente?",
                    Answer = "Para criar sua conta de cliente:\n\n1. Clique em \"Cadastrar\" no topo da página\n2. Preencha seus dados pessoais\n3. Confirme seu e-mail\n4. Pronto! Você já pode fazer reservas\n\nÉ rápido e gratuito! ⚡",
                    Keywords = "cadastro,cadastrar,cliente,conta,criar conta,novo usuário,registrar",
                    Intentions = "cadastro_cliente",
                    DisplayOrder = 2,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },
                new ChatFaqItem
                {
                    Category = "Cadastro",
                    Question = "Quais documentos preciso para me cadastrar como parceiro?",
                    Answer = "Documentos necessários para cadastro de parceiros:\n\n✅ CNPJ da empresa\n✅ Contrato Social ou CNPJ completo\n✅ Comprovante de endereço do estabelecimento\n✅ RG e CPF do representante legal\n✅ Fotos do espaço de coworking\n\nTodos os documentos devem estar atualizados. 📄",
                    Keywords = "documentos,cadastro,parceiro,preciso,necessário,cnpj,contrato social",
                    Intentions = "cadastro_parceiro",
                    DisplayOrder = 3,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },
                new ChatFaqItem
                {
                    Category = "Reservas",
                    Question = "Como fazer uma reserva?",
                    Answer = "Para fazer uma reserva:\n\n1. Faça login na plataforma\n2. Busque coworkings pela localização desejada\n3. Escolha o espaço e a data\n4. Selecione o horário\n5. Confirme e efetue o pagamento\n\nVocê receberá a confirmação por e-mail! 📧",
                    Keywords = "reserva,reservar,fazer,agendar,booking,marcar,como reservar",
                    Intentions = "consultar_reserva",
                    DisplayOrder = 4,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },
                new ChatFaqItem
                {
                    Category = "Reservas",
                    Question = "Posso cancelar uma reserva?",
                    Answer = "Sim! Você pode cancelar sua reserva:\n\n• Até 24h antes: reembolso de 100%\n• Entre 12-24h antes: reembolso de 50%\n• Menos de 12h: sem reembolso\n\nPara cancelar, acesse \"Minhas Reservas\" no menu. ❌",
                    Keywords = "cancelar,reserva,desistir,reembolso,devolver",
                    Intentions = "cancelamento",
                    DisplayOrder = 5,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },
                new ChatFaqItem
                {
                    Category = "Reservas",
                    Question = "Como consultar minhas reservas?",
                    Answer = "Para consultar suas reservas:\n\n1. Faça login na plataforma\n2. Acesse o menu \"Minhas Reservas\"\n3. Você verá todas as reservas: ativas, passadas e canceladas\n\nVocê também pode receber notificações por e-mail. 📱",
                    Keywords = "consultar,reservas,minhas,ver,visualizar,histórico",
                    Intentions = "consultar_reserva",
                    DisplayOrder = 6,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },
                new ChatFaqItem
                {
                    Category = "Reservas",
                    Question = "Posso alterar a data ou horário da minha reserva?",
                    Answer = "Sim! Você pode alterar sua reserva:\n\n1. Acesse \"Minhas Reservas\"\n2. Selecione a reserva que deseja alterar\n3. Clique em \"Alterar\"\n4. Escolha nova data/horário\n\nAs mesmas regras de cancelamento se aplicam. 🔄",
                    Keywords = "alterar,mudar,modificar,reserva,data,horário,trocar",
                    Intentions = "consultar_reserva",
                    DisplayOrder = 7,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },
                new ChatFaqItem
                {
                    Category = "Pagamentos",
                    Question = "Quais formas de pagamento são aceitas?",
                    Answer = "Aceitamos as seguintes formas de pagamento:\n\n💳 Cartão de crédito (Visa, Mastercard, Elo)\n💰 PIX (aprovação instantânea)\n\nPagamentos via PIX têm desconto especial! 🎉",
                    Keywords = "pagamento,pagar,forma,método,cartão,pix,aceita",
                    Intentions = "pagamento",
                    DisplayOrder = 8,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },
                new ChatFaqItem
                {
                    Category = "Pagamentos",
                    Question = "Quando o parceiro recebe o pagamento?",
                    Answer = "Os parceiros recebem os pagamentos:\n\n• Via transferência bancária\n• A cada 15 dias (quinzenal)\n• Após dedução da taxa de intermediação da Klyon\n\nVocê pode acompanhar seus recebimentos no painel administrativo. 💰",
                    Keywords = "pagamento,receber,parceiro,transferência,quando,prazo",
                    Intentions = "pagamento",
                    DisplayOrder = 9,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },
                new ChatFaqItem
                {
                    Category = "Pagamentos",
                    Question = "Qual a taxa cobrada pela Klyon?",
                    Answer = "A Klyon cobra uma taxa de intermediação de:\n\n• 15% sobre o valor de cada reserva\n\nEssa taxa cobre:\n✅ Plataforma e tecnologia\n✅ Suporte ao cliente\n✅ Marketing e divulgação\n✅ Processamento de pagamentos\n✅ Segurança das transações",
                    Keywords = "taxa,cobrar,comissão,percentual,valor,quanto,klyon",
                    Intentions = "informacao_preco",
                    DisplayOrder = 10,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },
                new ChatFaqItem
                {
                    Category = "Endereço Fiscal",
                    Question = "O que é endereço fiscal?",
                    Answer = "O endereço fiscal é:\n\n📍 Um domicílio fiscal fornecido pela Klyon aos clientes\n📍 Permite usar o endereço do coworking como sede da empresa\n📍 Inclui serviços como:\n  • Recebimento de correspondências\n  • Atendimento telefônico\n  • Declaração de domicílio fiscal\n\nÉ ideal para empresas que precisam de um endereço comercial! 🏢",
                    Keywords = "endereço fiscal,domicílio,fiscal,o que é,sede",
                    Intentions = "endereco_fiscal",
                    DisplayOrder = 11,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },
                new ChatFaqItem
                {
                    Category = "Endereço Fiscal",
                    Question = "Como contratar o serviço de endereço fiscal?",
                    Answer = "Para contratar endereço fiscal:\n\n1. Escolha um coworking parceiro que oferece o serviço\n2. Selecione o plano de endereço fiscal\n3. Envie a documentação necessária\n4. Assine o contrato\n5. Receba a declaração de domicílio fiscal\n\nO processo leva em média 3-5 dias úteis. ⏱️",
                    Keywords = "contratar,endereço fiscal,como,fazer,solicitar",
                    Intentions = "endereco_fiscal",
                    DisplayOrder = 12,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },
                new ChatFaqItem
                {
                    Category = "Informações",
                    Question = "Onde a Klyon atua?",
                    Answer = "A Klyon está presente em diversas cidades brasileiras! 🇧🇷\n\nNossas principais regiões:\n• São Paulo (capital e interior)\n• Rio de Janeiro\n• Belo Horizonte\n• Brasília\n• E mais cidades em expansão!\n\nBusque por coworkings na sua região através da nossa plataforma. 📍",
                    Keywords = "onde,localização,cidades,região,atua,opera,brasil",
                    Intentions = "localizacao",
                    DisplayOrder = 13,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },
                new ChatFaqItem
                {
                    Category = "Informações",
                    Question = "Como funciona a plataforma Klyon?",
                    Answer = "A Klyon é uma plataforma que:\n\n🔗 Conecta coworkings parceiros com clientes\n💻 Facilita reservas de espaços de trabalho\n📊 Oferece gestão completa para parceiros\n💳 Processa pagamentos de forma segura\n🏢 Disponibiliza serviços de endereço fiscal\n\nTudo em um só lugar, de forma simples e transparente! ✨",
                    Keywords = "como funciona,plataforma,klyon,o que é,sobre",
                    Intentions = "saudacao,informacao_preco",
                    DisplayOrder = 14,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },
                new ChatFaqItem
                {
                    Category = "Informações",
                    Question = "Qual o horário de atendimento da Klyon?",
                    Answer = "Nosso horário de atendimento:\n\n🕐 Segunda a Sexta: 9h às 18h\n🕐 Sábado: 9h às 13h\n❌ Domingo e feriados: Fechado\n\nCanais de atendimento:\n📧 E-mail: suporte@klyon.com.br\n📱 WhatsApp: (11) 9xxxx-xxxx\n\nO KlyonBoot está disponível 24/7! 🤖",
                    Keywords = "horário,atendimento,funciona,abre,fecha,contato",
                    Intentions = "horario_funcionamento",
                    DisplayOrder = 15,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },
                new ChatFaqItem
                {
                    Category = "Suporte",
                    Question = "Como entrar em contato com o suporte?",
                    Answer = "Para falar com nosso suporte:\n\n📧 E-mail: suporte@klyon.com.br\n📱 WhatsApp: (11) 9xxxx-xxxx\n💬 Chat: Disponível na plataforma\n\nTempo médio de resposta:\n• Chat/WhatsApp: 5-10 minutos\n• E-mail: até 24h\n\nEstamos aqui para ajudar! 🤝",
                    Keywords = "suporte,contato,ajuda,falar,atendimento,problema",
                    Intentions = "suporte_tecnico",
                    DisplayOrder = 16,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                }
            };
        }
    }
}