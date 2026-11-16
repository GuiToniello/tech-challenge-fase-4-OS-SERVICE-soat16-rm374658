namespace TechChallenge.Oficina.OsService.UseCases.Features.Eventos;

// Processado: o evento alterou a ordem de servico. Ignorado: ordem inexistente, transicao nao permitida (ex.: reentrega) ou tipo desconhecido.
// Nos dois casos a mensagem pode sair da fila; so uma excecao faz o evento ser reprocessado.
public enum ResultadoProcessamentoEvento
{
    Processado,
    Ignorado
}
