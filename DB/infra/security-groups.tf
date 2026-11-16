# Regras como recursos separados (sem blocos inline), como no DB/infra do repo MANAGEMENT.
# So os nodes do EKS (pods do OS Service) acessam este banco.

resource "aws_security_group" "rds" {
  name        = "${local.name_prefix}-rds-sg"
  description = "Private PostgreSQL access from EKS nodes (OS Service)"
  vpc_id      = data.aws_vpc.this.id
}

resource "aws_vpc_security_group_ingress_rule" "rds_from_eks_nodes" {
  security_group_id            = aws_security_group.rds.id
  description                  = "PostgreSQL from EKS nodes"
  referenced_security_group_id = data.aws_security_group.eks_nodes.id
  from_port                    = 5432
  to_port                      = 5432
  ip_protocol                  = "tcp"
}

resource "aws_vpc_security_group_egress_rule" "rds_all" {
  security_group_id = aws_security_group.rds.id
  cidr_ipv4         = "0.0.0.0/0"
  ip_protocol       = "-1"
}
