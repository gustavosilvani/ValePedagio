# AGENTS.md

Instrucoes especificas do `ValePedagio`.

## Papel deste repositorio

- dominio operacional especializado de Vale-Pedagio
- ownership de cotacao, compra, cancelamento, recibo, status, callback e configuracao por tenant

## Regras

- nao mover integracoes de operadoras para o `MicroServico`
- nao transformar o `core-cte` em adapter direto de provedores
- o `core-cte` consome este servico e pode importar o resultado para o MDF-e
- manter a matriz de provedores por wave explicita
- tratar contrato regulatorio do MDF-e como saida do dominio, nao como dono do ciclo operacional
