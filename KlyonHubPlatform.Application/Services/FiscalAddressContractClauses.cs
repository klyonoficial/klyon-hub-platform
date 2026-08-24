using KlyonHubPlatform.Application.Helpers;

namespace KlyonHubPlatform.Application.Services
{
    public static class FiscalAddressContractClauses
    {
        public const string IntroParagraph =
            "As partes acima identificadas resolvem firmar o presente Contrato de Uso de Endereço Fiscal, que se regerá pelas cláusulas e condições abaixo.";

        public const string ClosingParagraphWithoutCnpj =
            "E, por estarem justas e contratadas, as partes firmam o presente contrato por meio eletrônico.";

        public const string ClosingParagraphWithCnpj =
            "E, por estarem justas e contratadas, as partes firmam o presente contrato eletronicamente.";

        public static string ClosingParagraph(FiscalAddressContractVariant variant) =>
            variant == FiscalAddressContractVariant.WithCnpj
                ? ClosingParagraphWithCnpj
                : ClosingParagraphWithoutCnpj;

        public static IReadOnlyList<string> GetClauses(FiscalAddressContractVariant variant, string paymentClauseText) =>
            new[]
            {
                GetClause1(variant),
                GetClause2(),
                GetClause3(),
                GetClause4(),
                GetClause5(variant),
                GetClause6(paymentClauseText),
                GetClause7(),
                GetClause8(variant),
                GetClause9(),
                GetClause10(),
                GetClause11(),
                GetClause12(),
                GetClause13(variant),
                GetClause14(),
                GetClause15()
            };

        public static string GetClause1(FiscalAddressContractVariant variant)
        {
            var objectIntro = variant == FiscalAddressContractVariant.WithoutCnpj
                ? "1.1. O presente contrato tem por objeto a cessão onerosa e temporária do endereço da CONTRATADA, exclusivamente para fins de domicílio fiscal e cadastral do CONTRATANTE, para constituição de futura pessoa jurídica, podendo ser utilizado para:"
                : "1.1. O presente contrato tem por objeto a cessão onerosa e temporária do endereço da CONTRATADA, exclusivamente para fins de domicílio fiscal e cadastral da CONTRATANTE, podendo ser utilizado para:";

            var bullets = variant == FiscalAddressContractVariant.WithCnpj
                ? """
• Registro, constituição e alteração do CNPJ perante a Receita Federal do Brasil;
• Cadastro e registro na Junta Comercial;
• Cadastro e regularização perante a Prefeitura Municipal;
• Cadastro junto a órgãos públicos federais, estaduais e municipais;
• Cadastro junto a instituições financeiras, bancos, meios de pagamento, fornecedores e marketplaces;
• Recebimento de correspondências administrativas e fiscais;
• Demais fins estritamente fiscais e cadastrais permitidos por lei.
"""
                : """
• Registro, constituição e alteração do CNPJ perante a Receita Federal do Brasil;
• Cadastro e registro na Junta Comercial;
• Cadastro e regularização perante a Prefeitura Municipal;
• Cadastro junto a órgãos públicos federais, estaduais e municipais;
• Recebimento de correspondências administrativas e fiscais;
• Demais fins estritamente fiscais e cadastrais permitidos por lei.
""";

            return $"""
CLÁUSULA 1ª — DO OBJETO
{objectIntro}
{bullets}
1.2. O presente contrato possui natureza exclusivamente administrativa e fiscal, não concedendo direito automático de uso físico do imóvel, nem caracterizando locação comercial, nos termos da Lei nº 8.245/1991.
1.3. O uso físico de salas, estações de trabalho, salas de reunião ou qualquer estrutura da CONTRATADA constitui serviço distinto e independente, devendo ser objeto de contratação específica, mediante disponibilidade e conforme tabela de preços vigente.
1.4. A CONTRATANTE que possuir contrato ativo de endereço fiscal poderá contratar o uso físico de espaços da CONTRATADA com condições comerciais diferenciadas, conforme política vigente e tabela de preços aplicável aos clientes de endereço fiscal, não estando tal benefício automaticamente incluído no presente contrato.
1.5. O presente contrato, por si só, não autoriza a CONTRATANTE a:
• Trabalhar fisicamente no endereço de forma permanente;
• Atender clientes presencialmente sem contratação específica de espaço;
• Manter estoque, mercadorias ou bens no local;
• Utilizar o endereço como sede operacional;
• Exercer atividade empresarial física sem contratação específica.
""";
        }

        public static string GetClause2() =>
            """
CLÁUSULA 2ª — DA NATUREZA DO SERVIÇO
2.1. O serviço prestado possui natureza estritamente administrativa, não implicando:
• Exercício de atividade econômica no local;
• Funcionamento operacional da empresa no endereço;
• Atendimento presencial ao público, salvo se contratado separadamente.
2.2. A CONTRATADA não se responsabiliza por atividades exercidas pela CONTRATANTE em nenhum momento.
""";

        public static string GetClause3() =>
            """
CLÁUSULA 3ª — DAS OBRIGAÇÕES DA CONTRATANTE
São obrigações da CONTRATANTE:
a) Utilizar o endereço exclusivamente para fins cadastrais e fiscais;
b) Manter seus dados atualizados junto aos órgãos públicos;
c) Não exercer atividade proibida, ilícita ou incompatível com o endereço;
d) Não receber mercadorias, estoques ou clientes no local;
e) Manter o pagamento em dia;
f) Promover alteração de endereço junto aos órgãos competentes quando solicitado ou quando ocorrer o encerramento do contrato.
""";

        public static string GetClause4() =>
            """
CLÁUSULA 4ª — DAS OBRIGAÇÕES DA CONTRATADA
São obrigações da CONTRATADA:
a) Autorizar o uso do endereço fiscal enquanto vigente o presente contrato e adimplente a CONTRATANTE;
b) Manter o imóvel regularizado perante a Prefeitura Municipal, inclusive com inscrição imobiliária válida e apta para fins cadastrais e fiscais;
c) Autorizar a utilização do endereço exclusivamente para fins fiscais e cadastrais, conforme objeto do contrato;
d) Receber correspondências físicas endereçadas à CONTRATANTE;
e) Proceder, mediante autorização expressa da CONTRATANTE, à abertura, digitalização e disponibilização das correspondências por meio eletrônico e/ou sistema próprio;
f) Manter sigilo e confidencialidade sobre as informações e correspondências da CONTRATANTE, exceto quando exigido por lei ou autoridade competente;
g) Notificar a CONTRATANTE em caso de:
• inadimplência;
• irregularidade cadastral;
• uso indevido do endereço;
• revogação ou encerramento do contrato;
h) Informar, quando solicitado por autoridade pública ou órgão fiscalizador, a condição contratual da CONTRATANTE em relação ao uso do endereço fiscal, nos termos da legislação vigente.
""";

        public static string GetClause5(FiscalAddressContractVariant variant)
        {
            if (variant == FiscalAddressContractVariant.WithoutCnpj)
            {
                return """
5ª CLÁUSULA — DO PRAZO E CONDIÇÃO PARA CONSTITUIÇÃO
5.1. O presente contrato é celebrado pelo prazo mínimo de 12 (doze) meses, condicionado à abertura da pessoa jurídica.
5.2. O CONTRATANTE deverá promover a abertura do CNPJ no prazo máximo de 30 (trinta) dias contados da assinatura.
5.3. Após a abertura do CNPJ, o contrato continuará vigente automaticamente, passando a ter como CONTRATANTE a pessoa jurídica constituída, mediante formalização de aditivo contratual.
5.4. O não cumprimento do prazo previsto implicará revogação automática do uso do endereço fiscal, independentemente de aviso prévio.
5.5. Após o término do prazo mínimo, o contrato passará a vigorar por prazo indeterminado, renovando-se automaticamente, independentemente de novo instrumento contratual.
5.6. A renovação ocorrerá automaticamente mediante a continuidade do pagamento do plano contratado.
5.7. O contrato permanecerá ativo enquanto:
• o pagamento estiver regular; e
• não houver solicitação formal de cancelamento.
5.8. Qualquer das partes poderá rescindir o contrato mediante aviso prévio de 30 dias, sem necessidade de justificativa conforme a CLÁUSULA 8ª — DA REVOGAÇÃO DO ENDEREÇO FISCAL.
5.9. Em caso de inadimplência superior a 5 dias, o contrato poderá ser suspenso ou rescindido automaticamente, com revogação do uso do endereço fiscal.
""";
            }

            return """
5ª CLÁUSULA — DO PRAZO E RENOVAÇÃO
5.1. O presente contrato é celebrado pelo prazo mínimo de 12 (doze) meses, contados da data de sua assinatura.
5.2. Após o término do prazo mínimo, o contrato passará a vigorar por prazo indeterminado, renovando-se automaticamente, independentemente de novo instrumento contratual.
5.3. A renovação ocorrerá automaticamente mediante a continuidade do pagamento do plano contratado.
5.4. O contrato permanecerá ativo enquanto:
• o pagamento estiver regular; e
• não houver solicitação formal de cancelamento.
5.5. Qualquer das partes poderá rescindir o contrato mediante aviso prévio de 30 dias, sem necessidade de justificativa conforme a CLÁUSULA 8ª — DA REVOGAÇÃO DO ENDEREÇO FISCAL.
5.6. Em caso de inadimplência superior a 5 dias, o contrato poderá ser suspenso ou rescindido automaticamente, com revogação do uso do endereço fiscal.
""";
        }

        public static string GetClause6(string paymentClauseText) =>
            $"""
CLÁUSULA 6ª — DO VALOR E PAGAMENTO
6.1. Pelo uso do endereço fiscal, a CONTRATANTE pagará o valor de {paymentClauseText}
6.2. O atraso superior a 5 dias caracteriza inadimplência.
""";

        public static string GetClause7() =>
            """
CLÁUSULA 7ª — DO REAJUSTE
O valor será reajustado anualmente, com base no índice IPCA, independentemente de assinatura de novo contrato.
""";

        public static string GetClause8(FiscalAddressContractVariant variant)
        {
            var revocationItems = variant == FiscalAddressContractVariant.WithoutCnpj
                ? """
a) Inadimplência da CONTRATANTE;
b) Não renovação do contrato;
c) Solicitação de cancelamento por parte da CONTRATANTE;
d) Uso indevido do endereço;
e) Exercício de atividade incompatível com a natureza do serviço contratado;
f) Determinação legal, administrativa ou por autoridade competente;
g) Descumprimento de qualquer obrigação prevista neste contrato.
h) Não abertura do CNPJ no prazo estabelecido.
"""
                : """
a) Inadimplência da CONTRATANTE;
b) Não renovação do contrato;
c) Solicitação de cancelamento por parte da CONTRATANTE;
d) Uso indevido do endereço;
e) Exercício de atividade incompatível com a natureza do serviço contratado;
f) Determinação legal, administrativa ou por autoridade competente;
g) Descumprimento de qualquer obrigação prevista neste contrato.
""";

            return $"""
CLÁUSULA 8ª — DA REVOGAÇÃO DO ENDEREÇO FISCAL
8.1. O uso do endereço fiscal será REVOGADO AUTOMATICAMENTE, independentemente de aviso prévio, nos seguintes casos:
{revocationItems}
8.2. Revogado o uso do endereço fiscal, a CONTRATANTE deverá promover, no prazo máximo de 30 (trinta) dias, a alteração do endereço de sua empresa junto à Receita Federal, Junta Comercial, Prefeitura e demais órgãos competentes, sob sua exclusiva responsabilidade.
8.3. A CONTRATADA poderá, a qualquer tempo, comunicar aos órgãos públicos competentes que a CONTRATANTE não possui mais autorização para uso do endereço fiscal, sem necessidade de autorização prévia ou posterior da CONTRATANTE.
8.4. A revogação, rescisão, cancelamento ou término do presente contrato, por qualquer motivo, inclusive por solicitação da CONTRATANTE, NÃO gera direito à devolução de quaisquer valores pagos, seja total ou parcialmente.
8.5. A CONTRATANTE reconhece e concorda expressamente que os valores pagos possuem natureza de remuneração pelo serviço prestado e pela disponibilidade e reserva do endereço fiscal, não sendo reembolsáveis em hipótese alguma, inclusive em caso de:
• Cancelamento solicitado pela CONTRATANTE;
• Encerramento da empresa;
• Alteração de endereço por iniciativa da CONTRATANTE;
• Indeferimento de abertura do CNPJ por órgãos públicos;
• Revogação por inadimplência ou infração contratual.
""";
        }

        public static string GetClause9() =>
            """
CLÁUSULA 9ª — DA RESPONSABILIDADE FISCAL E LEGAL
9.1. A CONTRATANTE é única e exclusiva responsável por:
• Tributos;
• Multas;
• Penalidades;
• Obrigações fiscais, trabalhistas, previdenciárias e ambientais.
9.2. A CONTRATADA não responde solidária ou subsidiariamente por qualquer obrigação da CONTRATANTE.
""";

        public static string GetClause10() =>
            """
CLÁUSULA 10ª — DA FISCALIZAÇÃO
10.1. Em caso de fiscalização por órgãos públicos, a CONTRATADA poderá:
• Informar se a empresa está ou não ativa no endereço;
• Apresentar documentos que comprovem a revogação do uso.
10.2. A CONTRATANTE autoriza expressamente tais comunicações.
""";

        public static string GetClause11() =>
            """
CLÁUSULA 11ª — DA RESCISÃO
11.1. O contrato poderá ser rescindido:
• Por qualquer das partes, mediante aviso prévio de 30 dias;
• Imediatamente, em caso de infração legal ou contratual.
""";

        public static string GetClause12() =>
            """
CLÁUSULA 12ª — DA CONFIDENCIALIDADE
12.1. As partes comprometem-se a manter sigilo sobre informações comerciais e cadastrais, exceto quando exigido por lei.
""";

        public static string GetClause13(FiscalAddressContractVariant variant)
        {
            if (variant == FiscalAddressContractVariant.WithoutCnpj)
            {
                return """
CLÁUSULA 13ª — DO USO LIMITADO DO ENDEREÇO FISCAL
O endereço fiscal objeto deste contrato é autorizado exclusivamente para a constituição de 01 (uma) pessoa jurídica, sendo vedado o uso para múltiplos CNPJ's sem novo contrato.
""";
            }

            return """
CLÁUSULA 13ª — DO USO LIMITADO DO ENDEREÇO FISCAL
O endereço fiscal objeto deste contrato é autorizado exclusivamente para a pessoa jurídica aqui identificada, sendo expressamente vedado o uso do endereço para quaisquer outros CNPJ's, ainda que pertencentes ao mesmo sócio, grupo econômico ou pessoa física, sem a celebração de novo contrato específico.
""";
        }

        public static string GetClause14() =>
            """
CLÁUSULA 14ª — DO RECEBIMENTO, ABERTURA E DIGITALIZAÇÃO DE CORRESPONDÊNCIAS
14.1. A CONTRATANTE autoriza expressamente a CONTRATADA a receber correspondências físicas endereçadas à pessoa física ou jurídica vinculada ao presente contrato, incluindo, mas não se limitando a comunicações de órgãos públicos, instituições financeiras, fornecedores e demais remetentes, observados os limites e condições dos itens 14.6 e 14.7.
14.2. A CONTRATANTE autoriza, de forma expressa e irrevogável enquanto vigente o contrato, a abertura das correspondências recebidas, exclusivamente para fins de:
• identificação do destinatário;
• verificação da natureza da correspondência;
• digitalização do conteúdo;
• disponibilização eletrônica por meio de sistema próprio da CONTRATADA e/ou envio ao e-mail cadastrado pela CONTRATANTE.
14.3. A CONTRATADA compromete-se a manter confidencialidade sobre o conteúdo das correspondências, utilizando as informações exclusivamente para fins de digitalização e disponibilização à CONTRATANTE.
14.4. A CONTRATADA não se responsabiliza por:
• atrasos na entrega por parte dos Correios ou transportadoras;
• extravio ocorrido antes do recebimento pela CONTRATADA;
• correspondências não identificadas com o nome empresarial ou nome do titular do contrato.
14.5. As correspondências físicas poderão ser armazenadas pela CONTRATADA pelo prazo de até 60 (sessenta) dias contados do recebimento. Findo esse prazo sem que a CONTRATANTE solicite retirada ou envio físico, a CONTRATADA poderá dar destinação às correspondências conforme sua política vigente (inclusive descarte), ressalvado o disposto no item 14.7.
14.6. Limites do serviço de recepção: O serviço de recepção de correspondências e encomendas no endereço fiscal fica limitado a:
• 1 (uma) entrega em formato de caixa ou pacote por mês, com dimensões máximas de 50 cm x 50 cm (cinquenta centímetros por cinquenta centímetros);
• cartas e envelopes em volume considerado razoável para uso de endereço fiscal, a critério da CONTRATADA.
O não cumprimento do prazo de retirada ou solicitação de envio previsto no item 14.5 poderá acarretar suspensão do recebimento de novas entregas em formato de caixa ou pacote até a regularização (retirada ou destinação da encomenda anterior). A CONTRATADA e seus prepostos ficam autorizados a recusar, nessas condições, novas entregas que excedam ou repitam o limite não cumprido.
14.7. Correspondência judicial: As correspondências de cunho judicial (citações, intimações, ofícios de juízo, notificações de órgãos públicos com força de intimação e assemelhados) não estão sujeitas à recusa e serão sempre recebidas, armazenadas e disponibilizadas à CONTRATANTE nos prazos e pela forma combinados, sem prejuízo de eventual aplicação de limites de armazenamento físico quando cabível.
14.8. A CONTRATANTE declara estar ciente de que o descumprimento dos limites e prazos desta cláusula pode resultar na recusa de novas entregas nos termos do item 14.6, sem que isso caracterize inadimplemento da CONTRATADA.
""";

        public static string GetClause15() =>
            """
CLÁUSULA 15ª — DO FORO
15.1. Fica eleito o foro da Comarca de Cariacica/ES, com renúncia a qualquer outro, por mais privilegiado que seja.
""";
    }
}
