output "rds_identifier" {
  value = aws_db_instance.this.identifier
}

output "rds_endpoint" {
  value = aws_db_instance.this.address
}

output "rds_port" {
  value = aws_db_instance.this.port
}

output "rds_database" {
  value = var.rds_db_name
}

output "rds_security_group_id" {
  value = aws_security_group.rds.id
}

output "dynamodb_historico_saga_nome" {
  value = aws_dynamodb_table.historico_saga.name
}

output "dynamodb_historico_saga_arn" {
  value = aws_dynamodb_table.historico_saga.arn
}
