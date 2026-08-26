using KlyonHubPlatform.Application.Helpers;



namespace KlyonHubPlatform.Application.Services

{

    public static class CommercialAddressContractClauses

    {

        public const string IntroParagraph =

            "As partes acima identificadas resolvem firmar o presente Contrato de Uso de Endereço Comercial, que se regerá pelas cláusulas e condições abaixo.";



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

                ? "1.1. O presente contrato tem por objeto a cessão onerosa e temporária do endereço da CONTRATADA, exclusivamente para fins de apoio comercial e institucional do CONTRATANTE, para constituição de futura pessoa jurídica, podendo ser utilizado para:"

                : "1.1. O presente contrato tem por objeto a cessão onerosa e temporária do endereço da CONTRATADA, exclusivamente para fins de apoio comercial e institucional da CONTRATANTE, podendo ser utilizado para:";



            var bullets = """

• Divulgação em cartão de visita, site, materiais de marketing e comunicações comerciais;

• Recebimento de correspondências comerciais e administrativas;

• Apoio administrativo e institucional contratado;

• Demais fins estritamente comerciais e institucionais permitidos por lei e por este contrato.

""";



            return $"""

CLÁUSULA 1ª - DO OBJETO

{objectIntro}

{bullets}
1.2. O presente contrato possui natureza exclusivamente comercial e administrativa, não concedendo direito automático de uso físico do imóvel, nem caracterizando locação comercial nos termos da Lei nº 8.245/1991.

1.3. O endereço objeto deste contrato NÃO poderá ser utilizado como domicílio fiscal, sede tributária ou endereço cadastral perante a Receita Federal, Junta Comercial, Prefeitura Municipal ou demais órgãos públicos.

1.4. O uso físico de salas, estações de trabalho, salas de reunião ou qualquer estrutura da CONTRATADA constitui serviço distinto e independente, devendo ser objeto de contratação específica, mediante disponibilidade e conforme tabela de preços vigente.

1.5. O presente contrato, por si só, não autoriza a CONTRATANTE a:

• Utilizar o endereço como sede fiscal ou cadastral;

• Registrar ou alterar CNPJ com base neste endereço;

• Exercer atividade física permanente no local sem contratação específica de espaço;

• Manter estoque, mercadorias ou bens no local sem autorização expressa.

""";

        }



        public static string GetClause2() =>

            """

CLÁUSULA 2ª - DA NATUREZA DO SERVIÇO

2.1. O serviço prestado possui natureza estritamente comercial e administrativa, não implicando:

• Exercício de atividade econômica física no local, salvo contratação específica;

• Funcionamento operacional da empresa no endereço;

• Autorização de uso fiscal ou cadastral.

2.2. A CONTRATADA não se responsabiliza por atividades exercidas pela CONTRATANTE em desacordo com este contrato.

""";



        public static string GetClause3() =>

            """

CLÁUSULA 3ª - DAS OBRIGAÇÕES DA CONTRATANTE

São obrigações da CONTRATANTE:

a) Utilizar o endereço exclusivamente para fins comerciais e institucionais previstos neste contrato;

b) Não utilizar o endereço como domicílio fiscal, tributário ou cadastral;

c) Manter seus dados comerciais atualizados junto a terceiros;

d) Não exercer atividade proibida, ilícita ou incompatível com o endereço;

e) Manter o pagamento em dia;

f) Comunicar imediatamente à CONTRATADA qualquer uso indevido ou tentativa de cadastro fiscal com o endereço.

""";



        public static string GetClause4() =>

            """

CLÁUSULA 4ª - DAS OBRIGAÇÕES DA CONTRATADA

São obrigações da CONTRATADA:

a) Autorizar o uso comercial do endereço enquanto vigente o presente contrato e adimplente a CONTRATANTE;

b) Manter o imóvel regularizado perante a Prefeitura Municipal;

c) Receber correspondências comerciais endereçadas à CONTRATANTE, conforme limites deste contrato;

d) Proceder, mediante autorização expressa da CONTRATANTE, à abertura, digitalização e disponibilização das correspondências por meio eletrônico e/ou sistema próprio;

e) Manter sigilo e confidencialidade sobre as informações e correspondências da CONTRATANTE, exceto quando exigido por lei ou autoridade competente;

f) Notificar a CONTRATANTE em caso de inadimplência, irregularidade ou uso indevido do endereço;

g) Informar, quando solicitado por autoridade pública, que o endereço não possui natureza fiscal para a CONTRATANTE, nos termos da legislação vigente.

""";



        public static string GetClause5(FiscalAddressContractVariant variant)

        {

            if (variant == FiscalAddressContractVariant.WithoutCnpj)

            {

                return """

5ª CLÁUSULA - DO PRAZO E CONDIÇÃO PARA CONSTITUIÇÃO

5.1. O presente contrato é celebrado pelo prazo mínimo de 12 (doze) meses, condicionado à abertura da pessoa jurídica.

5.2. O CONTRATANTE deverá promover a abertura do CNPJ no prazo máximo de 30 (trinta) dias contados da assinatura, sem utilizar o endereço como domicílio fiscal.

5.3. Após a abertura do CNPJ, o contrato continuará vigente automaticamente, passando a ter como CONTRATANTE a pessoa jurídica constituída, mediante formalização de aditivo contratual.

5.4. O não cumprimento do prazo previsto implicará revogação automática do uso do endereço comercial, independentemente de aviso prévio.

5.5. Após o término do prazo mínimo, o contrato passará a vigorar por prazo indeterminado, renovando-se automaticamente, independentemente de novo instrumento contratual.

5.6. A renovação ocorrerá automaticamente mediante a continuidade do pagamento do plano contratado.

5.7. Qualquer das partes poderá rescindir o contrato mediante aviso prévio de 30 dias, conforme a CLÁUSULA 8ª - DA REVOGAÇÃO DO ENDEREÇO COMERCIAL.

5.8. Em caso de inadimplência superior a 5 dias, o contrato poderá ser suspenso ou rescindido automaticamente, com revogação do uso do endereço comercial.

""";

            }



            return """

5ª CLÁUSULA - DO PRAZO E RENOVAÇÃO

5.1. O presente contrato é celebrado pelo prazo mínimo de 12 (doze) meses, contados da data de sua assinatura.

5.2. Após o término do prazo mínimo, o contrato passará a vigorar por prazo indeterminado, renovando-se automaticamente, independentemente de novo instrumento contratual.

5.3. A renovação ocorrerá automaticamente mediante a continuidade do pagamento do plano contratado.

5.4. O contrato permanecerá ativo enquanto o pagamento estiver regular e não houver solicitação formal de cancelamento.

5.5. Qualquer das partes poderá rescindir o contrato mediante aviso prévio de 30 dias, conforme a CLÁUSULA 8ª - DA REVOGAÇÃO DO ENDEREÇO COMERCIAL.

5.6. Em caso de inadimplência superior a 5 dias, o contrato poderá ser suspenso ou rescindido automaticamente, com revogação do uso do endereço comercial.

""";

        }



        public static string GetClause6(string paymentClauseText) =>

            $"""

CLÁUSULA 6ª - DO VALOR E PAGAMENTO

6.1. Pelo uso do endereço comercial, a CONTRATANTE pagará o valor de {paymentClauseText}

6.2. O atraso superior a 5 dias caracteriza inadimplência.

""";



        public static string GetClause7() =>

            """

CLÁUSULA 7ª - DO REAJUSTE

O valor será reajustado anualmente, com base no índice IPCA, independentemente de assinatura de novo contrato.

""";



        public static string GetClause8(FiscalAddressContractVariant variant)

        {

            var revocationItems = variant == FiscalAddressContractVariant.WithoutCnpj

                ? """

a) Inadimplência da CONTRATANTE;

b) Não renovação do contrato;

c) Solicitação de cancelamento por parte da CONTRATANTE;

d) Uso indevido do endereço, inclusive como endereço fiscal ou cadastral;

e) Exercício de atividade incompatível com a natureza comercial do serviço contratado;

f) Determinação legal, administrativa ou por autoridade competente;

g) Descumprimento de qualquer obrigação prevista neste contrato;

h) Não abertura do CNPJ no prazo estabelecido, quando aplicável.

"""

                : """

a) Inadimplência da CONTRATANTE;

b) Não renovação do contrato;

c) Solicitação de cancelamento por parte da CONTRATANTE;

d) Uso indevido do endereço, inclusive como endereço fiscal ou cadastral;

e) Exercício de atividade incompatível com a natureza comercial do serviço contratado;

f) Determinação legal, administrativa ou por autoridade competente;

g) Descumprimento de qualquer obrigação prevista neste contrato.

""";



            return $"""

CLÁUSULA 8ª - DA REVOGAÇÃO DO ENDEREÇO COMERCIAL

8.1. O uso do endereço comercial será REVOGADO AUTOMATICAMENTE, independentemente de aviso prévio, nos seguintes casos:

{revocationItems}
8.2. Revogado o uso do endereço comercial, a CONTRATANTE deverá promover, no prazo máximo de 30 (trinta) dias, a retirada do endereço de materiais comerciais, site, cartões de visita e demais meios de divulgação, sob sua exclusiva responsabilidade.

8.3. A CONTRATADA poderá, a qualquer tempo, comunicar a terceiros e autoridades competentes que a CONTRATANTE não possui mais autorização para uso comercial do endereço.

8.4. A revogação, rescisão, cancelamento ou término do presente contrato, por qualquer motivo, NÃO gera direito à devolução de quaisquer valores pagos, seja total ou parcialmente.

8.5. A CONTRATANTE reconhece que os valores pagos possuem natureza de remuneração pelo serviço prestado e pela disponibilidade do endereço comercial, não sendo reembolsáveis em hipótese alguma.

""";

        }



        public static string GetClause9() =>

            """

CLÁUSULA 9ª - DA RESPONSABILIDADE COMERCIAL E LEGAL

9.1. A CONTRATANTE é única e exclusiva responsável por suas obrigações comerciais, fiscais, trabalhistas, previdenciárias e ambientais decorrentes de sua atividade empresarial.

9.2. A CONTRATADA não responde solidária ou subsidiariamente por qualquer obrigação da CONTRATANTE.

""";



        public static string GetClause10() =>

            """

CLÁUSULA 10ª - DA FISCALIZAÇÃO

10.1. Em caso de fiscalização ou solicitação de terceiros, a CONTRATADA poderá informar se a empresa possui ou não autorização comercial vigente para uso do endereço.

10.2. A CONTRATANTE autoriza expressamente tais comunicações.

""";



        public static string GetClause11() =>

            """

CLÁUSULA 11ª - DA RESCISÃO

11.1. O contrato poderá ser rescindido:

• Por qualquer das partes, mediante aviso prévio de 30 dias;

• Imediatamente, em caso de infração legal ou contratual, inclusive uso do endereço como domicílio fiscal.

""";



        public static string GetClause12() =>

            """

CLÁUSULA 12ª - DA CONFIDENCIALIDADE

12.1. As partes comprometem-se a manter sigilo sobre informações comerciais e cadastrais, exceto quando exigido por lei.

""";



        public static string GetClause13(FiscalAddressContractVariant variant)

        {

            if (variant == FiscalAddressContractVariant.WithoutCnpj)

            {

                return """

CLÁUSULA 13ª - DO USO LIMITADO DO ENDEREÇO COMERCIAL

O endereço comercial objeto deste contrato é autorizado exclusivamente para a constituição de 01 (uma) pessoa jurídica, sendo vedado o uso para múltiplos CNPJ's sem novo contrato.

""";

            }



            return """

CLÁUSULA 13ª - DO USO LIMITADO DO ENDEREÇO COMERCIAL

O endereço comercial objeto deste contrato é autorizado exclusivamente para a pessoa jurídica aqui identificada, sendo expressamente vedado o uso do endereço para quaisquer outros CNPJ's sem a celebração de novo contrato específico.

""";

        }



        public static string GetClause14() =>

            """

CLÁUSULA 14ª - DO RECEBIMENTO, ABERTURA E DIGITALIZAÇÃO DE CORRESPONDÊNCIAS

14.1. A CONTRATANTE autoriza expressamente a CONTRATADA a receber correspondências comerciais endereçadas à pessoa física ou jurídica vinculada ao presente contrato.

14.2. A CONTRATANTE autoriza, de forma expressa e irrevogável enquanto vigente o contrato, a abertura das correspondências recebidas, exclusivamente para fins de identificação, digitalização e disponibilização eletrônica.

14.3. A CONTRATADA compromete-se a manter confidencialidade sobre o conteúdo das correspondências.

14.4. A CONTRATADA não se responsabiliza por atrasos ou extravios ocorridos antes do recebimento pela CONTRATADA.

14.5. As correspondências físicas poderão ser armazenadas pela CONTRATADA pelo prazo de até 60 (sessenta) dias contados do recebimento.

14.6. Correspondências judiciais serão sempre recebidas e disponibilizadas à CONTRATANTE nos prazos combinados.

""";



        public static string GetClause15() =>

            """

CLÁUSULA 15ª - DO FORO

15.1. Fica eleito o foro da Comarca de Cariacica/ES, com renúncia a qualquer outro, por mais privilegiado que seja.

""";

    }

}

