# e-Frete sandbox contract summary

Status: implementation-ready summary for the first sandbox integration in `ValePedagio`.

Sources used:
- public e-Frete integration references reachable from this environment
- public legacy SOAP manual references for `LogonService.asmx` and transport/payment services
- public community reports confirming a separate SOAP service for vale-pedagio, with login based on `usuario`, `senha` and `hash de integrador`

Important limitation:
- the official sandbox endpoints were timing out from this environment on April 17, 2026, so the request/response envelope below is the bounded source of truth for the codebase, but still needs one final homologation pass against the official WSDL/manual as soon as connectivity is available

## What is treated as confirmed

- transport is SOAP 1.1 over HTTPS
- authentication can be satisfied by either:
  - a fixed token already provisioned by the operator
  - a login SOAP call using `integratorHash`, `username` and `password`
- the adapter supports four synchronous operations:
  - quote
  - purchase
  - cancel
  - receipt
- tenant isolation lives in `ValePedagio` and not in the provider contract
- the provider configuration remains dynamic and tenant-scoped

## Runtime configuration keys

The `Credentials` map for `EFrete` now reserves these keys:

- `integratorHash`
- `username`
- `password`
- `token`
- `providerDocument`
- `documentType`
- `timeoutSeconds`
- `logonServicePath`
- `logonNamespace`
- `logonOperation`
- `logonAction`
- `loginVersion`
- `operationNamespace`
- `quoteServicePath`
- `quoteOperation`
- `quoteAction`
- `quoteVersion`
- `quoteRequestTemplate`
- `purchaseServicePath`
- `purchaseOperation`
- `purchaseAction`
- `purchaseVersion`
- `purchaseRequestTemplate`
- `cancelServicePath`
- `cancelOperation`
- `cancelAction`
- `cancelVersion`
- `cancelRequestTemplate`
- `receiptServicePath`
- `receiptOperation`
- `receiptAction`
- `receiptVersion`
- `receiptRequestTemplate`
- `loginRequestTemplate`

## Default SOAP behavior in code

- login:
  - service path default: `LogonService.asmx`
  - operation default: `Login`
- operational service path default:
  - `ValePedagioService.asmx`
- operation defaults:
  - quote: `CalcularRota`
  - purchase: `ComprarValePedagio`
  - cancel: `CancelarValePedagio`
  - receipt: `ObterReciboValePedagio`

The adapter sends a standard SOAP envelope and can use either:
- the default request template shipped in code
- a provider-specific template configured in `Credentials`

This makes the integration pluggable without another code change once the official e-Frete homologation XML is confirmed.

## Placeholders available to request templates

- `IntegratorHash`
- `Token`
- `Version`
- `TransportadorId`
- `MotoristaId`
- `VeiculoId`
- `DocumentoResponsavelPagamento`
- `EstimatedCargoValue`
- `Observacoes`
- `NumeroCompra`
- `Protocolo`
- `UfOrigem`
- `UfDestino`
- `UfsPercurso`
- `PontosParada`
- `CteIds`

## Response fields parsed by the adapter

The adapter currently extracts the first matching XML node by local name from the SOAP body for:

- auth:
  - `Token`
- operation identity:
  - `Protocolo`
  - `ProtocoloServico`
  - `NumeroCompra`
  - `CodigoCompra`
  - `NumeroOperacao`
- totals:
  - `ValorTotal`
  - `Valor`
  - `TotalPedagio`
- receipt:
  - `Arquivo`
  - `Pdf`
  - `Recibo`
  - `Filename`
  - `NomeArquivo`
  - `MimeType`
  - `Mimetype`
- status/message:
  - `Sucesso`
  - `Mensagem`
  - `faultstring`

## Final homologation checklist

- confirm the exact vale-pedagio WSDL/service path in sandbox
- replace default operation names if the homologation manual differs
- paste the official XML request templates in tenant configuration if the default template does not match the homologated contract
- confirm whether purchase already returns the receipt or if receipt must always be fetched in a second call
- validate the provider document and `documentType` used to populate the MDF-e regulatory payload
