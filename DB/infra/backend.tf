terraform {
  backend "s3" {
    bucket       = "terraform-state-soat16"
    key          = "techchallenge-oficina/fase-4/os-service-database.tfstate"
    region       = "us-east-1"
    encrypt      = true
    use_lockfile = true
  }
}
