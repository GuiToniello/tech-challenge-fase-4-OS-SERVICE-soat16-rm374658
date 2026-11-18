# ADR-019: OS Service, Microsserviço com Banco Próprio

**Status**: Aceita (fase 4). Atualizada com a entrada do Billing (ver **Atualização (Billing)**, abaixo)

**Resumo**:
O ciclo de vida da ordem de serviço (OS) sai da Management API e vira um microsserviço próprio, o **OS Service**, com banco, namespace no Kubernetes e pipelines próprios. A Management continua com os cadastros, o estoque e o e-mail de mudança de status; o orçamento, que também ficava lá, foi depois para o Billing ([ADR-021](https://github.com/GuiToniello/tech-challenge-fase-4-BILLING-soat16-rm374658/blob/main/docs/adr/ADR-021-Billing-Service-Orcamento-e-Pagamentos-com-Banco-Proprio.md)). Os serviços conversam só por eventos no Amazon SQS ([ADR-020](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/adr/ADR-020-Eventos-com-SQS-e-Saga-Coreografada.md)).

> As ADRs e RFCs são numeradas em sequência única entre os repositórios [MANAGEMENT](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658), OS-SERVICE (este), [BILLING](https://github.com/GuiToniello/tech-challenge-fase-4-BILLING-soat16-rm374658) e [LAMBDA](https://github.com/GuiToniello/tech-challenge-fase-4-LAMBDA-soat16-rm374658). O índice de todas as decisões está no [docs/arquitetura.md do MANAGEMENT](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/arquitetura.md#5-decisões-de-arquitetura-rfcs-e-adrs).

**Contexto**:
- Na fase 4, o sistema passa a ter microsserviços. O primeiro a sair da Management API é o ciclo de vida da OS: abertura, atualização, mudanças de status, consultas e indicadores.
- Até aqui, tudo ficava num só serviço e num só banco ([ADR-018](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/adr/ADR-018-Monorepo-e-API-Unica-Management.md)): a OS tinha chaves estrangeiras para clientes, veículos e serviços, e o orçamento ficava dentro da OS.
- A separação precisa:
  - definir o que fica em cada serviço, sem duplicar regras;
  - dar a cada serviço o próprio banco (*database per service*);
  - manter os padrões de código das fases anteriores;
  - caber no mesmo cluster pequeno (2 nós `t3.small`, [ADR-016](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/adr/ADR-016-Escalabilidade-com-HPA.md)), com custo baixo.

**Decisão**:

1. **Fronteiras**:
   - **OS Service** (este repositório): o ciclo de vida da OS (abrir, atualizar enquanto `Recebida`, excluir, consultar, listar por prioridade, acompanhar, listar por cliente e mudar o status) e os indicadores (tempo médio de execução e de entrega, recalculados a cada entrega).
   - **Management** (repo MANAGEMENT): clientes, veículos, serviços, insumos e estoque ([ADR-012](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/adr/ADR-012-Controle-de-Estoque-de-Insumos.md)), a validação da OS e o e-mail de mudança de status. Na versão original desta ADR, o orçamento e o e-mail dele também ficavam aqui.
   - **Billing** (repo BILLING, desde a entrada do Billing): o orçamento (gerar, enviar por e-mail, aprovar e recusar) e o pagamento pelo Mercado Pago (ADR-021 e [ADR-022](https://github.com/GuiToniello/tech-challenge-fase-4-BILLING-soat16-rm374658/blob/main/docs/adr/ADR-022-Pagamentos-com-Mercado-Pago.md)).
   - A rota de abertura completa (`POST /api/ordens-servico/completa`), que cadastrava cliente, veículo e serviços junto com a OS, foi removida: a OS é aberta com os ids de cadastros que já existem na Management.

2. **Um banco por serviço**: RDS PostgreSQL próprio (`techchallenge-oficina-os-postgres`, banco `oficina_os`), criado em `DB/infra`, nas subnets privadas da rede criada pelo MANAGEMENT. Nenhum serviço lê o banco do outro. As tabelas são criadas pelas migrations do OS Service, no startup:

   | Tabela | O que guarda | Colunas |
   |---|---|---|
   | `ordens_servico` | Ordens de serviço | `id` (chave), `cliente_id`, `veiculo_id`, `status`, `valor_orcamento` (opcional), `motivo_cancelamento` (opcional, até 300 caracteres) e `servico_ids` (`uuid[]`) |
   | `ordens_servico_historico_status` | Cada mudança de status de uma OS | `id` (número gerado pelo banco), `ordem_servico_id` (chave estrangeira, `CASCADE`), `status` e `data_alteracao` |
   | `indicadores` | Tempos médios das OS entregues | `id` (sempre 1), `tempo_medio_execucao` e `tempo_medio_entrega` |

   ```mermaid
   erDiagram
     ordens_servico ||--|{ ordens_servico_historico_status : "registra"
     indicadores
   ```

3. **A OS guarda só os ids dos cadastros**: `clienteId`, `veiculoId` e `servicoIds`, além do status, do histórico, do valor do orçamento e do motivo do cancelamento. Quem confere se o cliente, o veículo (do cliente) e os serviços existem é a Management, ao receber o evento `OrdemServicoAberta`. Se algo não existir, ela publica `OrdemServicoRejeitada`, e o OS Service cancela a OS: novo status `Cancelada` (8), a compensação da saga (ADR-020).

4. **O orçamento fica fora do OS Service** (hoje, no Billing): a OS guarda só o valor (`valorOrcamento`), recebido no evento `OrcamentoGerado`, que leva a OS para `AguardandoAprovacao`. O `PagamentoAprovado` leva a OS para `EmExecucao`, e o `OrcamentoRecusado`, para `Finalizada`.

5. **Infraestrutura e pipelines próprios**:
   - namespace `os-service` no mesmo EKS, com o Deployment `os-service-api` (Service `NodePort` 30081, atrás da rota `/os` do API Gateway) e um HPA de 1 a 3 pods (eram 5 antes do Billing);
   - imagem `techchallenge-oficina-os-service` no ECR (repositório criado manualmente);
   - workflows Bootstrap, Deploy, Destroy e K8s Apply ([.github/workflows/README.md](../../.github/workflows/README.md)).
   - A rede, o EKS, o API Gateway, o Datadog e as filas SQS continuam no MANAGEMENT; este repositório só os usa. A chave de acesso do OS Service às filas vem do SSM Parameter Store.

6. **Os mesmos padrões de código da Management**: Clean Architecture com folder-by-feature, Gateways, Commands/Queries/ViewModels, AutoMapper, EF Core com PostgreSQL e migrations no startup, Swagger, tratamento de erros em duas camadas e logs JSON com Serilog para o Datadog (ADR-001 a ADR-014, no repo MANAGEMENT). Os projetos se chamam `TechChallenge.Oficina.OsService.*`.

**Consequências**:
- ✅ O ciclo de vida da OS evolui, escala e é publicado sem depender da Management. Se a Management cair, a OS continua sendo aberta e consultada, e os eventos esperam na fila.
- ✅ Cada banco tem um dono: mudar a tabela de OS não afeta a Management nem a Lambda `buscar-cliente`.
- ✅ Código familiar: os mesmos padrões e a mesma organização da Management.
- ⚠ **Consistência eventual**: a OS nasce `Recebida` e só depois a Management valida os dados. Uma OS com um cliente inexistente aparece por alguns segundos antes de ser cancelada.
- ⚠ **Sem integridade entre os bancos**: excluir na Management um cliente, veículo ou serviço usado por uma OS não é bloqueado; a OS continua com o id antigo.
- ⚠ **Consultas só com ids**: a OS não traz nomes nem placas, que ficam na Management. `GET /api/ordens-servico/cliente/{clienteId}` não confere se o cliente existe e devolve uma lista vazia se não houver OS.
- ⚠ **Mais peças para operar**: outro RDS (cobrado por hora), outro repositório ECR, outro conjunto de pipelines e secrets, e uma ordem entre os repositórios (Bootstrap do MANAGEMENT antes do deste; Destroy deste antes do MANAGEMENT).
- ⚠ **Cluster mais apertado**: as APIs dividem os 2 nós, e o máximo do HPA ficou em 5 pods por API e, com o Billing, em 3 (ADR-016).
- ⚠ **Monitores de Kubernetes do Datadog** (CPU, memória, restarts e réplicas) continuam restritos ao namespace `oficina`; o OS Service aparece no APM, nos logs, no `http_check` e no synthetic ([ADR-014](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/adr/ADR-014-Observabilidade-com-Datadog.md)).

**Atualização (Billing)**:
- O orçamento saiu da Management e foi para o Billing, um terceiro serviço com banco próprio (ADR-021). O Billing recebe da Management os serviços, os preços e o estoque da OS no evento `OrdemServicoValidada`, que este serviço recebe e ignora.
- O evento de aprovação do orçamento, que vinha da Management, deixou de existir. No lugar dele, o Billing publica o `PagamentoAprovado` quando o pagamento é aprovado no Mercado Pago (por consulta ou webhook), e só então a OS vai para `EmExecucao`. A etapa do log dessa transição continua `orcamento_aprovacao`. O `OrcamentoGerado` e o `OrcamentoRecusado` continuam iguais, agora publicados pelo Billing.
- A rota `POST /api/ordens-servico/{id}/em-execucao` passou a aceitar só a OS em `EmDiagnostico` (sem orçamento): com orçamento, o pagamento não pode ser pulado.
- Cada evento da OS vai, com o mesmo envelope, para as filas da Management e do Billing (`SqsSettings:FilasPublicacao`, *fan-out* no publicador, ADR-020). O consumidor para o lote na primeira mensagem que falha, para não processar eventos seguintes da mesma OS fora de ordem.

**Alternativas Rejeitadas**:
- Manter a OS na Management (monólito modular):
  - rejeitada porque a fase 4 pede serviços independentes, cada um com banco e deploy próprios.
- Levar também o orçamento para o OS Service:
  - rejeitada porque o orçamento depende dos preços dos serviços e insumos e do estoque (ADR-012), que são da Management. No OS Service, ele precisaria de uma cópia desses dados ou de chamadas à Management.
- Replicar os cadastros no OS Service (uma cópia local de clientes, veículos e serviços, alimentada por eventos):
  - rejeitada por exigir mais eventos e tabelas para sincronizar. A validação na Management, por evento, basta para o escopo. Fica como evolução.
- Banco ou schema compartilhado entre os serviços:
  - rejeitada porque manteria o acoplamento pelo banco (migrations e tabelas de um serviço afetando o outro), que é o que a separação quer remover.
- Validar o cliente, o veículo e os serviços por HTTP na Management, ao abrir a OS:
  - rejeitada por acoplar a disponibilidade dos dois serviços (a abertura falharia com a Management fora) e por exigir chamadas internas autenticadas. A comparação está na [RFC-004](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/rfc/RFC-004-Comunicacao-entre-Microsservicos.md).

**Relacionados**: [ADR-016](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/adr/ADR-016-Escalabilidade-com-HPA.md), [ADR-017](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/adr/ADR-017-PostgreSQL-no-Amazon-RDS-e-Modelo-de-Dados.md), [ADR-018](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/adr/ADR-018-Monorepo-e-API-Unica-Management.md), [ADR-020](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/adr/ADR-020-Eventos-com-SQS-e-Saga-Coreografada.md) e [RFC-004](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/rfc/RFC-004-Comunicacao-entre-Microsservicos.md), no repo MANAGEMENT; [ADR-021](https://github.com/GuiToniello/tech-challenge-fase-4-BILLING-soat16-rm374658/blob/main/docs/adr/ADR-021-Billing-Service-Orcamento-e-Pagamentos-com-Banco-Proprio.md) e [ADR-022](https://github.com/GuiToniello/tech-challenge-fase-4-BILLING-soat16-rm374658/blob/main/docs/adr/ADR-022-Pagamentos-com-Mercado-Pago.md), no repo BILLING.
