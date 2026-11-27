# Observabilidade (OS Service)

O OS Service segue o mesmo contrato de logs da Management API. A fonte da verdade (formato do JSON, chaves, regras, valores permitidos e o mapa evento → métrica → dashboard) é o [docs/observabilidade.md do repo MANAGEMENT](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/observabilidade.md). O Datadog (Agent, métricas de log, monitores, synthetics e dashboard) é criado pelo Terraform do MANAGEMENT, em `kubernetes/infra/addons` ([DATADOG.md](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/kubernetes/infra/DATADOG.md)): este repositório não tem nada do Datadog para configurar.

Aqui ficam só as particularidades deste serviço. As do Billing (orçamento e pagamento) estão no [docs/observabilidade.md do repo BILLING](https://github.com/GuiToniello/tech-challenge-fase-4-BILLING-soat16-rm374658/blob/main/docs/observabilidade.md), e as do Execução e Produção (fila de produção, eventos `execucao.*`), no [docs/observabilidade.md do repo EXECUTION](https://github.com/GuiToniello/tech-challenge-fase-4-EXECUTION-AND-PRODUCTION-soat16-rm374658/blob/main/docs/observabilidade.md). **Ao mudar um nome ou valor de evento**, mude também os catálogos da Management, do Billing e do Execução e Produção, os documentos e, se a mudança afetar métricas, monitores ou o dashboard, o Terraform do MANAGEMENT.

## 1. Serviço

| Projeto | `service` | Namespace | Prefixo no API Gateway |
|---|---|---|---|
| `TechChallenge.Oficina.OsService.API` | `os-service-api` | `os-service` | `/os` |

- As tags `service` e `env` (`study`) vêm das labels `tags.datadoghq.com/service` e `tags.datadoghq.com/env` do [Deployment](../kubernetes/k8s/features/os-service-api/deployment.yml).
- O Datadog Agent coleta o stdout dos pods e injeta o tracer .NET nos pods do namespace `os-service` (APM), sem mudança no código nem no Dockerfile.
- O Serilog é configurado em [LoggingExtension.cs](../server/1%20-%20Frameworks%20%26%20Drivers/TechChallenge.Oficina.OsService.API/Extensions/LoggingExtension.cs), igual ao da Management: 1 JSON por linha no stdout, com `timestamp`, `level`, `message`, `evento`, `logger.name`, `dd.*`, `error.*` e as propriedades dos eventos. As requisições a `/health*` não geram log.

## 2. Eventos emitidos

O catálogo fica em [OrdemServicoLogEventos.cs](../server/3%20-%20Application%20Business%20Rules/TechChallenge.Oficina.OsService.UseCases/Features/OrdensServico/Logging/OrdemServicoLogEventos.cs), com os mesmos nomes e `EventId` da Management.

| Evento (`EventId`) | Nível | Onde é emitido | Propriedades |
|---|---|---|---|
| `os.criada` (1001) | Information | `OrdemServicoUseCases.CriarAsync`, depois de gravar e antes de publicar `OrdemServicoAberta` | `OrdemServicoId`, `ClienteId`, `VeiculoId`, `QuantidadeServicos` |
| `os.status_alterado` (1002) | Information | Depois de gravar cada mudança de status: pela rota de entrega (`OrdemServicoUseCases`) e pelos eventos do Execução e Produção, do Billing e da Management (`OrdemServicoEventosUseCases`). O orçamento gerado de novo só atualiza o valor e não gera este evento | `OrdemServicoId`, `StatusAnterior`, `StatusNovo`, `Etapa`, `TempoNoStatusAnteriorSegundos` (número) |
| `os.encerrada` (1003) | Information | `OrdemServicoUseCases.AlterarStatusParaEntregueAsync`, depois de gravar. OS encerrada é a OS entregue | `OrdemServicoId`, `TempoTotalSegundos` (número) |
| `os.falha` (2001) | Warning | `OrdensServicoController`, nas ações que alteram a OS pela rota: abertura, atualização, exclusão e entrega | `Etapa`, `OrdemServicoId` (`null` na abertura), `TipoFalha`, `Motivo` |
| `integracao.falha` (3001) | Warning | `HistoricoSagaGateway.RegistrarAsync` (DynamoDB), ao gravar um item do histórico da saga: registra e **não** relança. O evento já foi tratado (no `Processado`, gravado no PostgreSQL e com o `OrdemServicoStatusAlterado` publicado, quando o status mudou), e o consumidor apaga a mensagem; só aquela linha falta no histórico da saga ([ADR-026](./adr/ADR-026-Historico-da-Saga-no-DynamoDB.md)) | `Integracao` = `dynamodb`, `Operacao` = `registro_historico`, `Tipo` (o evento consumido, como `DiagnosticoIniciado`), `OrdemServicoId`, `error.*` |
| `integracao.falha` (3001) | Error | `EventosPublisher`, se o envio do evento a uma das filas falhar (`Operacao` = `publicacao`): registra, com a `Fila` que falhou, e relança, e a requisição responde 500 com a alteração já gravada. As filas anteriores da lista já receberam o evento. `SqsConsumidorHostedService`, se falhar ao obter a fila, receber ou processar uma mensagem (`Operacao` = `consumo`): a mensagem volta para a fila, e as seguintes do lote também, para manter a ordem por OS. `HistoricoSagaGateway.ListarAsync` (DynamoDB), se a consulta do histórico da saga falhar (`Operacao` = `consulta_historico`): registra e relança, e a rota `/historico-saga` responde 500 | `Integracao` (`sqs` ou `dynamodb`), `Operacao`, `error.*` e, conforme o caso, `Tipo`, `OrdemServicoId`, `EventoId`, `MensagemId` e `Fila` |
| `evento.publicado` (4001) | Information | `EventosPublisher`, depois de enviar o evento a cada fila de destino (Management, Billing e Execução e Produção): um log por fila, com o mesmo `EventoId` | `Tipo`, `EventoId`, `Fila`, `OrdemServicoId` |
| `evento.consumido` (4002) | Information | `SqsConsumidorHostedService`, evento da Management, do Billing ou do Execução e Produção processado | `Tipo`, `EventoId`, `Fila` |
| `evento.ignorado` (4003) | Warning | `SqsConsumidorHostedService`: OS inexistente, transição que o status atual não permite (evento repetido ou fora de ordem) ou tipo que este serviço não trata, como o `OrdemServicoValidada`, que é para o Billing e o Execução e Produção. A mensagem é apagada. Antes dele, um log `Information` sem `EventId` explica o motivo, quando o evento é de uma OS, e o motivo também fica no histórico da saga da OS ([ADR-026](./adr/ADR-026-Historico-da-Saga-no-DynamoDB.md)) | `Tipo`, `EventoId`, `Fila` |
| `http.erro_nao_tratado` (9001) | Error | [GlobalExceptionHandlerMiddleware](../server/1%20-%20Frameworks%20%26%20Drivers/TechChallenge.Oficina.OsService.API/Middleware/GlobalExceptionHandlerMiddleware.cs): o 500 da falha de publicação numa requisição e da falha na consulta do histórico da saga, entre outros | `RequestMethod`, `RouteTemplate`, `Integracao` = `postgres` só com o banco indisponível, `error.*` |
| `app.migracao_falhou` (9002) | Critical (`Fatal` no JSON) | [Program.cs](../server/1%20-%20Frameworks%20%26%20Drivers/TechChallenge.Oficina.OsService.API/Program.cs), em volta de `ApplyMigrations()`. A API encerra com código 1 | `error.*` |

Logs sem `EventId` que ajudam no diagnóstico (todos `Information`): o motivo do `evento.ignorado` (OS não encontrada ou transição que o status atual não permite), que também fica no histórico da saga, e o início e o fim do consumidor da fila. Só localmente (com `DynamoDbSettings:ServiceUrl`), ao subir, sai também um `Information` com a tabela do histórico da saga criada ou já existente no DynamoDB Local, ou um `Warning` se não conseguir criá-la (a API sobe mesmo assim).

`os.orcamento_gerado`, `os.orcamento_enviado` e os eventos de pagamento não saem daqui: o orçamento e o pagamento são do Billing. Os eventos da fila de produção (`execucao.*`) também não: saem do Execução e Produção, que não emite `os.status_alterado`; a mudança de status do diagnóstico e da finalização é registrada aqui, quando este serviço consome `DiagnosticoIniciado` e `ExecucaoFinalizada`.

## 3. Etapas, integrações e status deste serviço

| `Etapa` | Origem | Transição em `os.status_alterado` |
|---|---|---|
| `abertura` | `POST /api/ordens-servico` | — (só `os.falha`) |
| `atualizacao` | `PUT /api/ordens-servico` | — (só `os.falha`) |
| `exclusao` | `DELETE /api/ordens-servico/{id}` | — (só `os.falha`) |
| `diagnostico` | Evento `DiagnosticoIniciado` (Execução e Produção) | `recebida` → `em_diagnostico` |
| `finalizacao` | Evento `ExecucaoFinalizada` (Execução e Produção) | `em_execucao` → `finalizada` |
| `entrega` | `POST /api/ordens-servico/{id}/entregar` | `finalizada` → `entregue` |
| `orcamento_geracao` | Evento `OrcamentoGerado` (Billing) | `recebida` ou `em_diagnostico` → `aguardando_aprovacao` |
| `orcamento_aprovacao` | Evento `PagamentoAprovado` (Billing; a etapa manteve o nome de antes) | `aguardando_aprovacao` → `em_execucao` |
| `orcamento_recusa` | Evento `OrcamentoRecusado` (Billing) | `aguardando_aprovacao` → `finalizada` |
| `cancelamento` | Evento `OrdemServicoRejeitada` (Management, compensação) | `recebida`, `em_diagnostico`, `aguardando_aprovacao` ou `em_execucao` → `cancelada` |

- A etapa `execucao` não é mais usada aqui: as rotas `/em-diagnostico`, `/em-execucao` e `/finalizar` saíram deste serviço com o Execução e Produção, e a OS só entra em `em_execucao` pelo `PagamentoAprovado` (etapa `orcamento_aprovacao`). As falhas do diagnóstico e da execução (`os.falha`) saem do Execução e Produção.
- O tempo em `em_execucao` (o `TempoNoStatusAnteriorSegundos` da `finalizacao`, usado no tempo médio por status do dashboard, e o `TempoMedioExecucao` de `GET /api/indicadores`) inclui a espera da OS na fila de produção do Execução e Produção, depois do pagamento.
- `Integracao`: `postgres` (banco indisponível), `sqs` (fila) e `dynamodb` (histórico da saga). Não há `resend`: este serviço não envia e-mail.
- `Operacao`: `publicacao` e `consumo` (SQS), `registro_historico` (Warning) e `consulta_historico` (Error) (DynamoDB). Os mesmos valores do histórico de pagamentos do Billing e da linha do tempo da produção do Execução e Produção.
- Status: os mesmos da Management, mais `cancelada` (`Cancelada` = 8). `encerrada` (7) existe no enum, mas nenhum fluxo usa.

## 4. Health checks

| Endpoint | Verifica o banco? | Quem usa |
|---|---|---|
| `/health` | Não | Probes do Kubernetes, NLB (target group `techchallenge-oficina-os`), `HEALTHCHECK` do Dockerfile e o synthetic `GET /os/health` pelo API Gateway |
| `/health/ready` | Sim (check `postgres`, tag `ready`): `Healthy` (200) ou `Unhealthy` (503) | Só o `http_check` do Datadog Agent, na instância `os-service-api` (service check `http.can_connect`) |

Os dois são anônimos. O `readinessProbe` fica em `/health`, para uma queda do banco não tirar os pods do Service. Nenhum dos dois verifica o SQS nem o DynamoDB (o `/health/ready` só verifica o PostgreSQL): as falhas deles aparecem no `integracao.falha` (`sqs` ou `dynamodb`).

## 5. O que o Datadog cobre deste serviço

- **APM (R1 e R4):** latência p95 e 5xx por rota, por `service`, nos mesmos monitores da Management.
- **Disponibilidade (R3):** `http.can_connect` do `os-service-api` e o synthetic `GET /os/health`, que entra no SLO de uptime.
- **Métricas de log (R7):** `oficina.os.criadas`, `oficina.os.encerradas` e `oficina.os.tempo_no_status` vêm dos eventos deste serviço. `oficina.os.falhas` e `oficina.integracao.erros` (por `integracao`, inclusive `dynamodb`) somam os quatro serviços. O filtro da `oficina.integracao.erros` (`@Integracao:*`) já conta as falhas do DynamoDB, inclusive o `Warning` do registro no histórico da saga; não há métricas nem monitores específicos do DynamoDB.
- **Limitação:** os monitores e widgets de Kubernetes (CPU, memória, CrashLoopBackOff, restarts e réplicas indisponíveis) filtram só o namespace `oficina`. Os pods deste serviço ficam de fora deles.

## 6. Consultas úteis

No Log Explorer do Datadog:

| Para ver | Consulta |
|---|---|
| Logs deste serviço | `service:os-service-api` |
| Erros (inclui o `Fatal`, que vira `emergency`) | `service:os-service-api status:(error OR emergency)` |
| Mudanças de status | `@evento:os.status_alterado service:os-service-api` |
| Diagnóstico e finalização vindos do Execução e Produção | `@evento:os.status_alterado @Etapa:(diagnostico OR finalizacao)` |
| OS canceladas pela saga | `@evento:os.status_alterado @StatusNovo:cancelada` |
| Eventos recebidos da Management, do Billing e do Execução e Produção | `@evento:evento.consumido service:os-service-api` |
| Eventos ignorados | `@evento:evento.ignorado service:os-service-api` |
| Filas para onde um evento foi publicado (um log por fila) | `@evento:evento.publicado @EventoId:<guid>` |
| Falhas no SQS | `@Integracao:sqs service:os-service-api` |
| Falhas no histórico da saga (DynamoDB) | `@Integracao:dynamodb service:os-service-api` |
| Linhas que faltam no histórico da saga | `@Integracao:dynamodb @Operacao:registro_historico service:os-service-api` |
| Histórico de uma OS nos quatro serviços | `@OrdemServicoId:<guid>` |

Localmente, filtre o stdout do serviço `os-service-api` do `docker-compose.yml`:

- Linux/macOS: `docker compose logs os-service-api | grep '"evento":"evento.consumido"'`
- Windows PowerShell: `docker compose logs os-service-api | Select-String '"evento":"evento.consumido"'`
