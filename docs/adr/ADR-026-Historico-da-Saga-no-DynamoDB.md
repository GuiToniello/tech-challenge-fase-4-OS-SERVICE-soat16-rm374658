# ADR-026: Histórico da Saga no Amazon DynamoDB

**Status**: Aceita (fase 4). Complementa a [ADR-019](ADR-019-OS-Service-Microsservico-com-Banco-Proprio.md), em que o PostgreSQL continua sendo o banco do OS Service, e segue o padrão do histórico de pagamentos do Billing ([ADR-024](https://github.com/GuiToniello/tech-challenge-fase-4-BILLING-soat16-rm374658/blob/main/docs/adr/ADR-024-Historico-de-Pagamentos-no-DynamoDB.md)) e da linha do tempo da produção do Execução e Produção ([ADR-025](https://github.com/GuiToniello/tech-challenge-fase-4-EXECUTION-AND-PRODUCTION-soat16-rm374658/blob/main/docs/adr/ADR-025-Linha-do-Tempo-da-Producao-no-DynamoDB.md))

**Resumo**:
O OS Service passa a guardar um **histórico da saga** de cada OS numa tabela do **Amazon DynamoDB**, um banco NoSQL: para cada evento da saga que ele consome e trata, o que ele fez com o evento (`Processado`, com o status antes e depois, ou `Ignorado`, com o motivo). O PostgreSQL continua sendo a fonte da verdade: a OS, o status, o histórico de status e todas as regras não mudam. O histórico da saga é complementar e descartável: a gravação é *best-effort*, os itens expiram em 180 dias, e nenhuma regra o lê para decidir algo. A consulta é feita em `GET /api/ordens-servico/{id}/historico-saga`.

> As ADRs e RFCs são numeradas em sequência única entre os repositórios [MANAGEMENT](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658), OS-SERVICE (este), [BILLING](https://github.com/GuiToniello/tech-challenge-fase-4-BILLING-soat16-rm374658), [EXECUTION](https://github.com/GuiToniello/tech-challenge-fase-4-EXECUTION-AND-PRODUCTION-soat16-rm374658) e [LAMBDA](https://github.com/GuiToniello/tech-challenge-fase-4-LAMBDA-soat16-rm374658). O índice das decisões está no [docs/arquitetura.md do MANAGEMENT](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/arquitetura.md#5-decisões-de-arquitetura-rfcs-e-adrs).

**Contexto**:
- O OS Service já guarda no PostgreSQL o histórico de status de cada OS (tabela `ordens_servico_historico_status`, ADR-019), lido pelo acompanhamento, pela listagem `/ordenadas` e pelos indicadores. Copiar esse histórico para o DynamoDB só duplicaria o dado.
- O que nenhum lugar guarda é o veredito do OS Service sobre cada evento consumido. Quando um evento é ignorado (OS inexistente, evento repetido ou fora de ordem), o `evento.ignorado` (4003) do consumidor não traz a OS nem o motivo: o motivo fica só num log `Information` sem `EventId`, logo antes. E um orçamento gerado de novo, com a OS já `AguardandoAprovacao`, só troca o valor, sem deixar rastro.
- Esse registro tem o formato típico de NoSQL, como o histórico de pagamentos do Billing (ADR-024) e a linha do tempo da produção do Execução e Produção (ADR-025): só recebe gravações novas (*append-only*), é lido sempre pela OS (uma chave), sem junções, os atributos variam conforme o resultado (o motivo só no evento ignorado, o status só quando a OS existe) e pode expirar sozinho.
- A [RFC-002](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/rfc/RFC-002-Escolha-do-Banco-de-Dados.md) rejeitou o DynamoDB como banco principal, porque o modelo da oficina é cheio de relacionamentos ([ADR-017](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/adr/ADR-017-PostgreSQL-no-Amazon-RDS-e-Modelo-de-Dados.md)). Isso continua valendo: aqui, ele entra só para um dado sem relacionamentos.
- Um banco por serviço ([ADR-020](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/adr/ADR-020-Eventos-com-SQS-e-Saga-Coreografada.md) e ADR-019): a tabela é só do OS Service, separada das tabelas do Billing e do Execução e Produção.
- É um projeto de estudo: o custo precisa ficar perto de zero, e o ambiente é destruído depois da demonstração.

**Decisão**:

1. **Papel de cada banco**:

   | Banco | O que guarda | Papel |
   |---|---|---|
   | PostgreSQL (RDS, banco `oficina_os`) | A OS (`ordens_servico`), o histórico de status (`ordens_servico_historico_status`) e os indicadores | **Fonte da verdade**. O status, o acompanhamento, a listagem `/ordenadas`, os indicadores e todas as regras saem dele, como antes |
   | DynamoDB (tabela `techchallenge-oficina-os-saga-historico`) | Um item por evento da saga consumido e tratado | **Histórico complementar e descartável**, só para consulta e auditoria. Nenhuma regra o lê; se faltar um item, nada quebra |

2. **Tabela**, em [DB/infra/dynamodb.tf](../../DB/infra/dynamodb.tf), no mesmo Terraform, state e pipeline do RDS:
   - sob demanda (`PAY_PER_REQUEST`), sem capacidade provisionada;
   - chave de partição `OrdemServicoId` (S) e chave de ordenação `OcorridoEm` (S): data e hora UTC em ISO-8601 com milissegundos, mais `#` e 8 caracteres hexadecimais (por exemplo, `2026-11-27T10:00:00.000Z#abcd1234`). A data ordena os eventos, e o sufixo evita colisão entre dois registros no mesmo milissegundo;
   - TTL no atributo `ExpiraEm` (epoch em segundos, preenchido pela API: data do processamento + 180 dias);
   - sem proteção contra exclusão (o Destroy apaga a tabela), sem PITR e sem streams;
   - outputs `dynamodb_historico_saga_nome` e `dynamodb_historico_saga_arn`.

3. **Item**, sem dados pessoais (do evento, só o tipo: o conteúdo não é guardado):

   | Atributo | Conteúdo |
   |---|---|
   | `Tipo` | O evento consumido: `OrdemServicoRejeitada`, `OrcamentoGerado`, `PagamentoAprovado`, `OrcamentoRecusado`, `DiagnosticoIniciado` ou `ExecucaoFinalizada` |
   | `Origem` | O serviço que publica o evento, fixo para cada tipo: `management` (`OrdemServicoRejeitada`), `billing` (`OrcamentoGerado`, `PagamentoAprovado` e `OrcamentoRecusado`) ou `execution` (`DiagnosticoIniciado` e `ExecucaoFinalizada`) |
   | `Resultado` | `Processado` (a transição foi aplicada) ou `Ignorado` |
   | `StatusAnterior` e `StatusNovo` | Os números do status, os mesmos do contrato dos eventos (`Recebida` 1 a `Cancelada` 8). Iguais quando o evento é ignorado ou quando o orçamento é gerado de novo; ausentes quando a OS não existe |
   | `Motivo` | Só no `Ignorado`: "Ordem de servico nao encontrada." ou a mensagem da regra de domínio que recusou a transição (por exemplo, "Somente ordem de servico recebida pode ser alterada para em diagnostico.") |

   O `OcorridoEm` é o instante em que o OS Service processou o evento: quando há transição, o mesmo instante gravado no histórico de status do PostgreSQL.

4. **Quando registra**: num só lugar, `OrdemServicoEventosUseCases.ProcessarAsync`, que trata os seis tipos, nas três saídas dele, sempre com `CancellationToken.None`, para o registro não se perder se o consumidor for encerrado:

   | Saída | O que registra |
   |---|---|
   | OS não encontrada | `Ignorado`, com o motivo "Ordem de servico nao encontrada.", depois do log `Information` que já existia |
   | A regra de domínio recusou a transição (`DomainException`: evento repetido ou fora de ordem) | `Ignorado`, com o status atual (anterior igual ao novo) e a mensagem da regra como motivo, depois do log `Information` que já existia |
   | Transição aplicada | `Processado`, com o status antes e depois, depois de gravar no PostgreSQL, de registrar o log `os.status_alterado` e de publicar o `OrdemServicoStatusAlterado` (quando o status mudou). No orçamento gerado de novo, os dois status são iguais |

   - Se a publicação do `OrdemServicoStatusAlterado` falhar, nada é registrado. A mensagem volta para a fila, e a nova entrega é recusada pela regra (a transição já foi aplicada) e registrada como `Ignorado`.
   - Os tipos que o OS Service não trata (como o `OrdemServicoValidada`, que chega pelo *fan-out*) param no `EventosController` e não são registrados. As ações pela rota (abrir, atualizar, excluir e entregar) também não. O `EventosController` e o consumidor SQS não mudaram.

5. **Best-effort**:
   - `RegistrarAsync` nunca lança: uma falha (DynamoDB fora do ar, permissão negada, tabela inexistente) gera `integracao.falha` (3001) como **Warning**, com `Integracao` = `dynamodb` e `Operacao` = `registro_historico` (mais `OrdemServicoId` e `Tipo`), e o fluxo segue: o consumidor apaga a mensagem. Só aquela linha falta no histórico.
   - A gravação é síncrona: com o DynamoDB fora do ar, cada evento só termina depois do *timeout* e das novas tentativas do SDK.
   - `ListarAsync` registra `integracao.falha` como **Error** (`Operacao` = `consulta_historico`) e relança a falha: a rota de consulta responde 500, pelo middleware global ([ADR-013](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/adr/ADR-013-Estrategia-de-Tratamento-de-Erros-em-Duas-Camadas.md)).
   - A consulta é um `Query` na partição da OS, em ordem crescente da chave de ordenação, com leitura consistente (`ConsistentRead`) e percorrendo todas as páginas.

6. **Rota**: `GET /api/ordens-servico/{id}/historico-saga`, com JWT (na AWS, `/os/api/ordens-servico/{id}/historico-saga`, pela rota `/os` que já existe). Responde 200 com a lista em ordem cronológica, ou com a lista vazia quando não há registros. Cada item tem `ocorridoEm`, `tipo`, `origem`, `resultado`, `statusAnterior`, `statusNovo` e `motivo`. A rota não consulta o PostgreSQL: uma OS desconhecida ou excluída também responde 200 (a OS excluída mantém o histórico até os itens expirarem).

7. **Código**, no padrão de porta e adapter do SQS (`IEventosPublisher`) e dos históricos do Billing e do Execução e Produção (ADR-024 e ADR-025):
   - porta `IHistoricoSagaGateway`, record `EventoHistoricoSaga` (propriedades `init`) e as constantes `OrigensHistoricoSaga` no projeto UseCases (`Features/Eventos/Interfaces`), com o `EventoHistoricoSagaViewModel` e o mapeamento no `OrdemServicoProfile`. A porta chega pelo construtor ao `OrdemServicoEventosUseCases` (gravação) e ao `OrdemServicoUseCases` (consulta, `ListarHistoricoSagaAsync`);
   - projeto novo `TechChallenge.Oficina.OsService.NoSql` (anel "1 - Frameworks & Drivers"), o único com o pacote `AWSSDK.DynamoDBv2` (4.0.*): `HistoricoSagaGateway`, a interface interna `IDynamoDbClient` com o `DynamoDbClientAdapter` (o único ponto que usa o SDK) e o `AddInfraNoSql`;
   - configuração `DynamoDbSettings`: `TabelaHistoricoSaga` (na AWS, `DynamoDbSettings__TabelaHistoricoSaga` no ConfigMap) e `ServiceUrl` (só localmente);
   - testes no projeto `TechChallenge.Oficina.OsService.NoSql.Tests`, com o SDK mockado; os testes de integração usam um histórico em memória.

8. **Credenciais e IAM, sem usuário nem chave novos**: o [DB/infra/iam.tf](../../DB/infra/iam.tf) anexa a política inline `dynamodb-historico-saga` (só `dynamodb:PutItem` e `dynamodb:Query`, no ARN da tabela) ao usuário IAM do OS Service que já existe, `techchallenge-oficina-os-service-sqs`, criado pelo `SQS/infra` do MANAGEMENT. A chave desse usuário já chega ao pod (SSM → Secret `os-service-api-secrets`) e passa a servir às filas e à tabela; no `_k8s-apply.yml`, o passo agora se chama "Adicionar a chave do usuário IAM do OS Service (SQS e DynamoDB) ao kubernetes/k8s/.env". Na AWS, a API nunca cria nem descreve a tabela (sem `CreateTable` e `DescribeTable`): ela é do Terraform. As tabelas e as políticas do Billing e do Execução e Produção ficam nos repositórios deles, em outros usuários: nenhum serviço acessa a tabela de outro. Se o MANAGEMENT recriar o usuário, a política some junto, e é preciso rodar o **Bootstrap** deste repositório de novo (o Deploy só reaplica o `DB/infra` quando os arquivos dele mudam).

9. **Ambiente local**: o `docker-compose.yml` sobe o DynamoDB Local (`amazon/dynamodb-local:3.1.0`, em memória, container `oficina-os-dynamodb`), na porta `8002` do host (`DYNAMODB_PORT`), porque o do Billing usa a `8000` e o do Execução e Produção, a `8001`. A API aponta para ele por `DynamoDbSettings__ServiceUrl`, com as mesmas credenciais fictícias do ElasticMQ; no `dotnet run`, o `appsettings.Development.json` usa `http://localhost:8002`. Só com `ServiceUrl` preenchido a API cria a tabela no startup (`GarantirTabelasLocaisAsync`), com as mesmas chaves e o mesmo TTL; uma falha aí só gera um aviso, e a API sobe.

**Consequências**:
- ✅ Dá para ver, por uma rota, o que o OS Service fez com cada evento da saga de uma OS, inclusive os ignorados (com o motivo) e o orçamento gerado de novo, sem mudar as tabelas do PostgreSQL nem as regras da OS.
- ✅ Fica mais fácil investigar a saga: um evento repetido, fora de ordem ou de uma OS que não existe aparece com o motivo, sem procurar o log sem `EventId`.
- ✅ Custo perto de zero: tabela sob demanda, poucos itens e expiração automática, sem instância ligada.
- ✅ Uma falha no DynamoDB não derruba a saga: o consumo dos eventos segue.
- ✅ O mesmo padrão do Billing e do Execução e Produção (ADR-024 e ADR-025): NoSQL onde o formato combina (gravação e leitura por chave) e relacional onde há relacionamentos, como recomenda a RFC-002.
- ⚠ **O histórico pode ter buracos**: uma falha de gravação perde aquela linha (fica só o log `integracao.falha`), uma falha na publicação do `OrdemServicoStatusAlterado` não registra o `Processado` (a nova entrega aparece como `Ignorado`), e os itens somem depois de 180 dias. O status e o histórico de status que valem continuam sendo os do PostgreSQL.
- ⚠ **Sem atomicidade entre os dois bancos**: as gravações são separadas. Se o processo cair entre elas, o histórico fica sem o registro.
- ⚠ **Mais latência com o DynamoDB fora do ar**: como a gravação é síncrona, o consumo de cada evento espera o *timeout* e as novas tentativas do SDK antes de seguir.
- ⚠ **A OS excluída mantém o histórico** até os itens expirarem: a exclusão não apaga nada no DynamoDB (os itens não têm dados pessoais).
- ⚠ **O monitor de erros de integração também alerta para `dynamodb`**: a métrica `oficina.integracao.erros` (filtro `@Integracao:*`, no MANAGEMENT) conta inclusive o Warning do registro ([observabilidade.md](../observabilidade.md)).
- ⚠ **Mais uma ligação com o MANAGEMENT**: a política fica no usuário IAM criado por ele; se o usuário for recriado, o Bootstrap deste repositório precisa rodar de novo. O usuário `terraform` da CI precisa de permissões no DynamoDB e de `iam:PutUserPolicy`, `iam:GetUserPolicy` e `iam:DeleteUserPolicy` (hoje ele é administrador).
- ⚠ **Mais uma peça local**: o DynamoDB Local roda em memória, e o histórico some quando o container reinicia.

**Alternativas Rejeitadas**:
- Copiar para o DynamoDB o histórico de status da OS:
  - rejeitada porque só duplicaria a tabela `ordens_servico_historico_status`, que já é lida pelo acompanhamento, pela listagem `/ordenadas` e pelos indicadores, sem trazer informação nova.
- Auditar no DynamoDB o cadastro da OS (abrir, atualizar e excluir):
  - rejeitada porque fica longe da saga e guardaria os ids do cliente e do veículo. O que hoje não tem registro consultável é o resultado dos eventos consumidos.
- Registrar também os eventos publicados pelo OS Service:
  - rejeitada porque o log `evento.publicado`, um por fila de destino, já cobre a publicação.
- Guardar o id e a origem do envelope do evento:
  - rejeitada porque exigiria mudar o `EventosController` e o consumidor SQS, que passam aos casos de uso só o contrato do evento. Cada tipo tem uma origem fixa, e o `EventoId` continua nos logs `evento.*`.
- Levar a OS (com o histórico de status) para o DynamoDB e tirar o RDS:
  - rejeitada porque a listagem por prioridade, o acompanhamento e os indicadores dependem de consultas no PostgreSQL, e o DynamoDB entraria no caminho crítico de todas as rotas e do consumidor SQS. O histórico da saga mostra o uso de NoSQL sem esse risco.
- Uma raiz de Terraform e um state só para a tabela:
  - rejeitada porque traria novos jobs nas pipelines e mais um check obrigatório. A tabela fica no `DB/infra`, no mesmo state e pipeline do RDS.
- Um usuário IAM e uma chave só para o DynamoDB:
  - rejeitada porque traria novos parâmetros no SSM, novas chaves no Secret e credenciais explícitas no código, sem ganho real: a política inline já limita o acesso a `PutItem` e `Query` nesta tabela.

**Fora do escopo**: o conteúdo dos eventos (valor do orçamento, id do pagamento e motivo da rejeição); os eventos publicados pelo OS Service, que o log `evento.publicado` já cobre; os tipos ignorados pelo tipo, que chegam pelo *fan-out*; o id e a origem do envelope; levar a OS ou o histórico de status para o NoSQL; PITR (backup contínuo); DynamoDB Streams; métricas e monitores específicos do DynamoDB no Datadog (o `integracao.falha` já alimenta a `oficina.integracao.erros`).

**Relacionados**: [ADR-019](ADR-019-OS-Service-Microsservico-com-Banco-Proprio.md) (este repo), [ADR-024](https://github.com/GuiToniello/tech-challenge-fase-4-BILLING-soat16-rm374658/blob/main/docs/adr/ADR-024-Historico-de-Pagamentos-no-DynamoDB.md) (repo BILLING), [ADR-025](https://github.com/GuiToniello/tech-challenge-fase-4-EXECUTION-AND-PRODUCTION-soat16-rm374658/blob/main/docs/adr/ADR-025-Linha-do-Tempo-da-Producao-no-DynamoDB.md) (repo EXECUTION), [ADR-004](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/adr/ADR-004-Gateways-e-Persistencia.md), [ADR-013](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/adr/ADR-013-Estrategia-de-Tratamento-de-Erros-em-Duas-Camadas.md), [ADR-014](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/adr/ADR-014-Observabilidade-com-Datadog.md), [ADR-017](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/adr/ADR-017-PostgreSQL-no-Amazon-RDS-e-Modelo-de-Dados.md), [ADR-020](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/adr/ADR-020-Eventos-com-SQS-e-Saga-Coreografada.md) e [RFC-002](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/rfc/RFC-002-Escolha-do-Banco-de-Dados.md), no repo MANAGEMENT.
