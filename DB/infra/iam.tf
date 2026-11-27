# Permissao da API do OS Service na tabela de historico da saga (dynamodb.tf).
# Anexada ao usuario IAM do OS Service que ja existe, criado pelo SQS/infra do repo MANAGEMENT
# (Bootstrap da MANAGEMENT antes, como hoje): o pod reaproveita a mesma chave de acesso (SSM -> Secret),
# sem chave ou secret novos. Politica inline com nome proprio, separada da politica "sqs" da MANAGEMENT,
# e so para esta tabela (as tabelas e as politicas do Billing e do Execucao e Producao ficam nos repos
# BILLING e EXECUTION, em outros usuarios).
# Se a MANAGEMENT recriar o usuario, a politica some junto: rode de novo o Bootstrap deste repo
# (reaplica o DB/infra, recriando a politica, e os manifests com a chave nova).

data "aws_iam_policy_document" "dynamodb" {
  statement {
    sid = "HistoricoSaga"
    actions = [
      "dynamodb:PutItem",
      "dynamodb:Query",
    ]
    resources = [aws_dynamodb_table.historico_saga.arn]
  }
}

resource "aws_iam_user_policy" "dynamodb" {
  name   = "dynamodb-historico-saga"
  user   = "${var.project_name}-os-service-sqs"
  policy = data.aws_iam_policy_document.dynamodb.json
}
