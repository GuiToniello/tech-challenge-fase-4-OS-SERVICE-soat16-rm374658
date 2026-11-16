# Observabilidade (OS Service)

O OS Service segue o mesmo contrato de logs da Management API. A fonte da verdade (formato do JSON, chaves, regras, valores permitidos e o mapa evento → métrica → dashboard) é o [docs/observabilidade.md do repo MANAGEMENT](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/docs/observabilidade.md). O Datadog (Agent, métricas de log, monitores, synthetics e dashboard) é criado pelo Terraform do MANAGEMENT, em `kubernetes/infra/addons` ([DATADOG.md](https://github.com/GuiToniello/tech-challenge-fase-4-MANAGEMENT-soat16-rm374658/blob/main/kubernetes/infra/DATADOG.md)): este repositório não tem nada do Datadog para configurar.

Aqui ficam só as particularidades deste serviço. **Ao mudar um nome ou valor de evento**, mude também o catálogo da Management, os dois documentos e, se a mudança afetar métricas, monitores ou o dashboard, o Terraform do MANAGEMENT.

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
| `os.status_alterado` (1002) | Information | Depois de gravar cada mudança de status: pelas rotas (`OrdemServicoUseCases`) e pelos eventos da Management (`OrdemServicoEventosUseCases`). O orçamento gerado de novo só atualiza o valor e não gera este evento | `OrdemServicoId`, `StatusAnterior`, `StatusNovo`, `Etapa`, `TempoNoStatusAnteriorSegundos` (número) |
| `os.encerrada` (1003) | Information | `OrdemServicoUseCases.AlterarStatusParaEntregueAsync`, depois de gravar. OS encerrada é a OS entregue | `OrdemServicoId`, `TempoTotalSegundos` (número) |
| `os.falha` (2001) | Warning | `OrdensServicoController`, nas ações que alteram a OS pela rota: abertura, atualização, exclusão e mudanças de status | `Etapa`, `OrdemServicoId` (`null` na abertura), `TipoFalha`, `Motivo` |
| `integracao.falha` (3001) | Error | `EventosPublisher`, se o envio do evento ao SQS falhar (`Operacao` = `publicacao`): registra e relança, e a requisição responde 500 com a alteração já gravada. `SqsConsumidorHostedService`, se falhar ao obter a fila, receber ou processar uma mensagem (`Operacao` = `consumo`): a mensagem volta para a fila | `Integracao` = `sqs`, `Operacao`, `error.*` e, conforme o caso, `Tipo`, `OrdemServicoId`, `EventoId`, `MensagemId` e `Fila` |
| `evento.publicado` (4001) | Information | `EventosPublisher`, depois de enviar o evento à fila da Management | `Tipo`, `EventoId`, `Fila`, `OrdemServicoId` |
| `evento.consumido` (4002) | Information | `SqsConsumidorHostedService`, evento da Management processado | `Tipo`, `EventoId`, `Fila` |
| `evento.ignorado` (4003) | Warning | `SqsConsumidorHostedService`: OS inexistente, transição que o status atual não permite (evento repetido ou fora de ordem) ou tipo que este serviço não trata. A mensagem é apagada. Antes dele, um log `Information` sem `EventId` explica o motivo, quando o evento é de uma OS | `Tipo`, `EventoId`, `Fila` |
| `http.erro_nao_tratado` (9001) | Error | [GlobalExceptionHandlerMiddleware](../server/1%20-%20Frameworks%20%26%20Drivers/TechChallenge.Oficina.OsService.API/Middleware/GlobalExceptionHandlerMiddleware.cs) | `RequestMethod`, `RouteTemplate`, `Integracao` = `postgres` só com o banco indisponível, `error.*` |
| `app.migracao_falhou` (9002) | Critical (`Fatal` no JSON) | [Program.cs](../server/1%20-%20Frameworks%20%26%20Drivers/TechChallenge.Oficina.OsService.API/Program.cs), em volta de `ApplyMigrations()`. A API encerra com código 1 | `error.*` |

`os.orcamento_gerado` e `os.orcamento_enviado` não saem daqui: o orçamento é da Management.

## 3. Etapas, integrações e status deste serviço

| `Etapa` | Origem | Transição em `os.status_alterado` |
|---|---|---|
| `abertura` | `POST /api/ordens-servico` | — (só `os.falha`) |
| `atualizacao` | `PUT /api/ordens-servico` | — (só `os.falha`) |
| `exclusao` | `DELETE /api/ordens-servico/{id}` | — (só `os.falha`) |
| `diagnostico` | `POST /api/ordens-servico/{id}/em-diagnostico` | `recebida` → `em_diagnostico` |
| `execucao` | `POST /api/ordens-servico/{id}/em-execucao` | `em_diagnostico` ou `aguardando_aprovacao` → `em_execucao` |
| `finalizacao` | `POST /api/ordens-servico/{id}/finalizar` | `em_execucao` → `finalizada` |
| `entrega` | `POST /api/ordens-servico/{id}/entregar` | `finalizada` → `entregue` |
| `orcamento_geracao` | Evento `OrcamentoGerado` | `recebida` ou `em_diagnostico` → `aguardando_aprovacao` |
| `orcamento_aprovacao` | Evento `OrcamentoAprovado` | `aguardando_aprovacao` → `em_execucao` |
| `orcamento_recusa` | Evento `OrcamentoRecusado` | `aguardando_aprovacao` → `finalizada` |
| `cancelamento` | Evento `OrdemServicoRejeitada` (compensação) | `recebida`, `em_diagnostico`, `aguardando_aprovacao` ou `em_execucao` → `cancelada` |

- `Integracao`: `postgres` (banco indisponível) e `sqs` (fila). Não há `resend`: este serviço não envia e-mail.
- `Operacao`: `publicacao` e `consumo`.
- Status: os mesmos da Management, mais `cancelada` (`Cancelada` = 8). `encerrada` (7) existe no enum, mas nenhum fluxo usa.

## 4. Health checks

| Endpoint | Verifica o banco? | Quem usa |
|---|---|---|
| `/health` | Não | Probes do Kubernetes, NLB (target group `techchallenge-oficina-os`), `HEALTHCHECK` do Dockerfile e o synthetic `GET /os/health` pelo API Gateway |
| `/health/ready` | Sim (check `postgres`, tag `ready`): `Healthy` (200) ou `Unhealthy` (503) | Só o `http_check` do Datadog Agent, na instância `os-service-api` (service check `http.can_connect`) |

Os dois são anônimos. O `readinessProbe` fica em `/health`, para uma queda do banco não tirar os pods do Service. Nenhum dos dois verifica o SQS: uma falha na fila aparece no `integracao.falha` (`sqs`).

## 5. O que o Datadog cobre deste serviço

- **APM (R1 e R4):** latência p95 e 5xx por rota, por `service`, nos mesmos monitores da Management.
- **Disponibilidade (R3):** `http.can_connect` do `os-service-api` e o synthetic `GET /os/health`, que entra no SLO de uptime.
- **Métricas de log (R7):** `oficina.os.criadas`, `oficina.os.encerradas` e `oficina.os.tempo_no_status` vêm dos eventos deste serviço. `oficina.os.falhas` e `oficina.integracao.erros` somam os dois serviços.
- **Limitação:** os monitores e widgets de Kubernetes (CPU, memória, CrashLoopBackOff, restarts e réplicas indisponíveis) filtram só o namespace `oficina`. Os pods deste serviço ficam de fora deles.

## 6. Consultas úteis

No Log Explorer do Datadog:

| Para ver | Consulta |
|---|---|
| Logs deste serviço | `service:os-service-api` |
| Erros (inclui o `Fatal`, que vira `emergency`) | `service:os-service-api status:(error OR emergency)` |
| Mudanças de status | `@evento:os.status_alterado service:os-service-api` |
| OS canceladas pela saga | `@evento:os.status_alterado @StatusNovo:cancelada` |
| Eventos recebidos da Management | `@evento:evento.consumido service:os-service-api` |
| Eventos ignorados | `@evento:evento.ignorado service:os-service-api` |
| Falhas no SQS | `@Integracao:sqs service:os-service-api` |
| Histórico de uma OS nos dois serviços | `@OrdemServicoId:<guid>` |

Localmente, filtre o stdout do serviço `os-service-api` do `docker-compose.yml`:

- Linux/macOS: `docker compose logs os-service-api | grep '"evento":"evento.consumido"'`
- Windows PowerShell: `docker compose logs os-service-api | Select-String '"evento":"evento.consumido"'`
