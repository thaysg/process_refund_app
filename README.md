# Processamento de Reembolsos

Solução para processar lotes de solicitações de reembolso, aplicar regras de negócio e antifraude e exibir os resultados em um aplicativo Flutter Android.

- **Backend (.NET/C#):** núcleo de regras compartilhado (`Reimbursement.Core`), API HTTP ASP.NET Core e CLI. API e CLI reutilizam o mesmo núcleo.
- **Aplicativo móvel:** Flutter (Android). O app envia o CSV e a data de referência para a API e exibe os resultados; ele **não** reimplementa cálculos nem regras de fraude.
- **Dados:** apenas dados fictícios. Tipos de procedimento e status permanecem em português conforme especificado.

## Estrutura do repositório

```text
/src/Reimbursement.Core          -> Regras de negócio (domínio, aplicação, CSV)
/src/Reimbursement.Api           -> API ASP.NET Core (camada fina sobre o Core)
/src/Reimbursement.Cli           -> Aplicação de console (stdin -> stdout, camada fina)
/tests/Reimbursement.Core.Tests  -> Testes do núcleo e da CLI
/tests/Reimbursement.Api.Tests   -> Testes de integração da API
/mobile/                         -> Aplicativo Flutter (Android)
/samples/                        -> CSVs de exemplo (dados fictícios)
/scripts/                        -> Scripts auxiliares (CLI e geração do .zip)
README.md
```

## Pré-requisitos (versões reais do ambiente)

Saídas reais verificadas neste ambiente:

- **.NET SDK:** `8.0.134` (`dotnet --version`) — target framework `net8.0`
- **Flutter:** `3.47.7` (stable), Tools • Dart `3.13.5` • DevTools `2.60.0` (`flutter --version`)
- **Dart SDK:** `3.13.5` (Tools • Dart, conforme `flutter --version`)
- **Android SDK / emulador ou dispositivo físico:** necessário para executar o app (`flutter run`)
- **JDK 17:** recomendado para builds Android

> Observação: se as dependências do Flutter não estiverem instaladas, execute `flutter pub get` em `/mobile`.

## Como executar

### API

Na raiz do repositório:

```bash
dotnet run --project src/Reimbursement.Api
```

- A URL de desenvolvimento é definida em `src/Reimbursement.Api/Properties/launchSettings.json` como `http://0.0.0.0:5000` (permite acesso pelo emulador Android em `http://10.0.2.2:5000`).
- Para sobrescrever a URL: `dotnet run --project src/Reimbursement.Api --urls http://0.0.0.0:5000` ou variável de ambiente `ASPNETCORE_URLS`.
- Swagger UI (ambiente Development): `http://localhost:5000/swagger`
- Health check: `GET http://localhost:5000/health`

### CLI

Lê CSV do **stdin**, escreve o CSV processado no **stdout** e erros/diagnósticos no **stderr**.

```bash
cat samples/sample.csv | dotnet run --project src/Reimbursement.Cli -- --reference-date 2024-05-01 > out.csv
```

Opções:
- `--reference-date <yyyy-MM-dd>`, `--reference-date=<yyyy-MM-dd>` ou `-r <yyyy-MM-dd>`: data de referência para janela de processamento e antifraude.
- Se não informada, usa a variável de ambiente `REFERENCE_DATE`; se ausente, usa a data local de hoje (`TimeProvider`).
- `-h`, `--help`: exibe ajuda e códigos de saída (retorna 0).

Códigos de saída:
- `0`: sucesso
- `1`: dados de entrada inválidos (inclui CSV vazio/estruturalmente inválido ou erros de validação)
- `2`: argumento inválido/desconhecido ou data inválida/ausente quando requerida pelo argumento

Exemplo de argumento desconhecido:

```bash
cat samples/sample.csv | dotnet run --project src/Reimbursement.Cli -- --reference-dat 2024-05-01
# stderr: mensagem de erro + uso; exit 2
```

Script auxiliar (requer `dotnet`): `./scripts/test-cli.sh`

### Aplicativo Flutter

Em `/mobile`:

```bash
cd mobile
flutter pub get
flutter run
```

- URL base padrão da API: `http://10.0.2.2:5000` (loopback do host para o emulador Android).
- Para sobrescrever: `flutter run --dart-define=API_BASE_URL=http://SEU_HOST:5000`

#### Apontando o app para a API local

- **Emulador Android:** usar `http://10.0.2.2:5000` (padrão).
- **Dispositivo físico:** usar o IP da máquina na rede local, por exemplo:
  ```bash
  flutter run --dart-define=API_BASE_URL=http://192.168.1.50:5000
  ```
  O dispositivo e a máquina devem estar na mesma rede e a porta 5000 deve estar acessível (firewall liberado).
- Em modo **debug**, a URL base é exibida no app (subtítulo/tooltip da barra superior) e incluída em mensagens de erro relacionadas à conexão para facilitar diagnóstico. Em builds de release essa informação não é exibida.
- A API usa HTTP (sem TLS) em desenvolvimento; o manifesto Android principal inclui `INTERNET` e `android:usesCleartextTraffic="true"` **apenas para desenvolvimento local com HTTP** (deve ser removido ou substituído por uma network security config em produção).

## Testes e análise

### .NET (núcleo + integração da API)

Na raiz do repositório:

```bash
dotnet build
dotnet test
```

Executa `tests/Reimbursement.Core.Tests` e `tests/Reimbursement.Api.Tests` (xUnit + FluentAssertions 6.12.0).

### Flutter

Em `/mobile`:

```bash
cd mobile
flutter analyze
flutter test
```

Resultados obtidos nesta sessão de polimento (comandos executados na raiz, exceto Flutter em `/mobile`):

- `dotnet build -warnaserror`: sucesso, 0 avisos e 0 erros.
- `dotnet test`: passed — 59 testes em `Reimbursement.Core.Tests` e 10 em `Reimbursement.Api.Tests` (69 no total, 0 falhas).
- `flutter pub get`: concluído.
- `flutter analyze`: nenhum problema encontrado (`No issues found!`).
- `flutter test`: passed — 22 testes, 0 falhas, incluindo os dois testes de escala de texto 2.0.
- `bash scripts/create-zip.sh`: pacote gerado com 0 entradas proibidas (`bin/`, `obj/`, `build/`, `.dart_tool/`, `node_modules/`, `dist/`, `.git/`, `.idea/`, `.gradle/`, `*.iml`, `local.properties`) e com `src/`, `tests/`, `samples/`, `mobile/lib`, `mobile/test`, `mobile/android`, `mobile/pubspec.yaml` e `README.md` presentes.

## Contrato da API

### `POST /api/reimbursements/process`

Processa um lote de reembolsos em CSV.

- **Content-Type:** `multipart/form-data`
  - `file`: arquivo CSV (obrigatório, não vazio)
  - `referenceDate`: `yyyy-MM-dd` (obrigatório)
- **Resposta em CSV:** `?format=csv` ou `Accept: text/csv` retorna o CSV processado (`text/csv; charset=utf-8`, arquivo `reimbursements.csv`).
- **Resposta padrão:** JSON com `referenceDate`, `summary` e `items`.
- **Erros de validação de CSV:** `400` com `application/problem+json` contendo `title`, `detail`, `status` e um array `errors` com objetos `{ "line": <n>, "field": "<campo ou null>", "message": "<mensagem>" }` para **todos** os problemas encontrados.
- **Outros erros 400:** requisição que não seja `multipart/form-data`, `referenceDate` ausente/inválido ou arquivo ausente/vazio retornam `400` (ProblemDetails/ValidationProblem).
- **Limite de upload:** requisições acima do limite configurado retornam `413` (ProblemDetails).
- **Erros inesperados:** `500`.

Campos JSON de cada item: `id`, `procedureType`, `procedureDate`, `amountPaid`, `reimbursedAmount`, `clientId`, `status`, `reason`, `reasonCode`.

Resumo (`summary`): `total`, `approved`, `rejected`, `suspectedFraud`, `totalReimbursed` (soma dos valores reembolsados dos itens **aprovados**).

#### Exemplo de resposta JSON

```json
{
  "referenceDate": "2024-05-01",
  "summary": {
    "total": 4,
    "approved": 3,
    "rejected": 1,
    "suspectedFraud": 0,
    "totalReimbursed": 960.0
  },
  "items": [
    {
      "id": 1,
      "procedureType": "Consulta Médica",
      "procedureDate": "2024-02-10",
      "amountPaid": 400,
      "reimbursedAmount": 320,
      "clientId": 123,
      "status": "Aprovado",
      "reason": "Aprovado",
      "reasonCode": "Approved"
    }
  ]
}
```

#### Exemplos com curl

JSON:

```bash
curl -X POST "http://localhost:5000/api/reimbursements/process" \
  -F "file=@samples/sample.csv;type=text/csv" \
  -F "referenceDate=2024-05-01"
```

CSV:

```bash
curl -X POST "http://localhost:5000/api/reimbursements/process?format=csv" \
  -F "file=@samples/sample.csv;type=text/csv" \
  -F "referenceDate=2024-05-01" \
  -o out.csv
```

ou via cabeçalho `Accept: text/csv`:

```bash
curl -X POST "http://localhost:5000/api/reimbursements/process" \
  -H "Accept: text/csv" \
  -F "file=@samples/sample.csv;type=text/csv" \
  -F "referenceDate=2024-05-01" \
  -o out.csv
```

Health:

```bash
curl http://localhost:5000/health
```

## Formato dos CSVs

- **Codificação:** UTF-8 (com ou sem BOM) na entrada; acentos preservados.
- **Data:** `yyyy-MM-dd`
- **Leitura robusta (RFC 4180 no texto completo):** campos entre aspas com vírgulas, aspas escapadas (`""`) e quebras de linha; BOM; CRLF/LF; linhas em branco ignoradas; nomes de colunas sem distinção de maiúsculas/minúsculas; colunas extras ignoradas; ordem de colunas flexível.
- **Erros estruturais (interrompem o parsing do arquivo, com mensagem clara):** aspas no meio de campo não citado (`Unexpected quote character inside an unquoted field.`), campo citado não fechado (`Unterminated quoted field.` indicando a linha de início do campo) e texto após aspas de fechamento.
- **Erros de validação:** todos os erros de linha/campo são reportados de uma vez (linha, campo e mensagem), incluindo `Id` duplicado.

### Entrada

Cabeçalho: `Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId`

Exemplo (`samples/sample.csv`): dados fictícios conforme o enunciado.

### Saída

Cabeçalho: `Id,TipoProcedimento,DataProcedimento,ValorPago,ValorReembolsado,ClienteId,Status`

Exemplo (`samples/sample-expected.csv`, data de referência `2024-05-01`) corresponde exatamente ao especificado.

- **Formato monetário na saída:** cultura invariável, ponto como separador decimal, sem separador de milhares. Valores inteiros sem casas decimais desnecessárias; quando houver fração, até 2 casas (ex.: `320`, `72.50`).
- **Status:** exatamente `Aprovado`, `Rejeitado`, `Suspeito de Fraude`.

## Decisões técnicas

- **Mesmo núcleo para API e CLI:** toda a regra de negócio fica em `Reimbursement.Core`, injetável via `RefundPolicy`, `IReimbursementCalculator` e `IReimbursementProcessor` (registrados por DI na API; CLI aceita processador injetável com caminho padrão).
- **Ordem de avaliação por solicitação:** (1) validação da entrada, (2) janela de 90 dias -> `Rejeitado`, (3) regras de fraude -> `Suspeito de Fraude`, caso contrário `Aprovado`.
- **Processamento cronológico, saída na ordem original:** por cliente, as solicitações são avaliadas em ordem cronológica (`DataProcedimento` ascendente, depois **ordem de entrada** como critério de desempate), de forma determinística e independente de valores de `Id`. O resultado é emitido na ordem de entrada.
- **Janela deslizante de antifraude (O(n) por cliente):** ao avançar cronologicamente, entradas aprovadas mais antigas que a janela de 30 dias são removidas de uma fila com contagem e total acumulados, preservando o mesmo comportamento das regras.
- **Janelas inclusivas:** uma data `d` está "dentro de N dias" de `t` quando `0 <= t - d <= N` (tanto para 90 quanto para 30 dias). Janela de 90 dias: `idade <= 90` dias. Janela de fraude (30 dias): `[DataProcedimento - 30, DataProcedimento]`.
- **Datas futuras:** se `DataProcedimento` for posterior à data de referência, a solicitação é **Rejeitada** (`ProcedureInFuture`) e o lote continua sendo processado. `ValorReembolsado` continua sendo calculado.
- **Tipo de procedimento desconhecido:** erro de validação (não é mapeado para `Outros`). Valores válidos: `Consulta Médica`, `Exame de Imagem`, `Exame Laboratorial`, `Outros`.
- **`Id` duplicado:** erro de validação.
- **Validação de valor (`ValorPago`):** deve ser numérico, maior que zero e com no máximo 2 casas decimais **pelo valor** (`decimal.Round(valor, 2) == valor`, aceitando representações como `10.100`), usando ponto como separador decimal e sem separador de milhares.
- **Todos os erros reportados:** o parser coleta todos os erros de linha/campo e os retorna juntos (CLI: `Line N [Field]: message` no stderr, exit 1; API: array `errors`).
- **Apenas `Aprovado` consome limites:** somente solicitações aprovadas entram na contagem e no total da janela de 30 dias. Solicitações rejeitadas ou suspeitas não consomem limites.
- **Limites de fraude e resolução de conflito:** máximo de 5 solicitações e R$ 1.500,00 reembolsados em 30 dias por cliente. O 6º pedido na janela (ou valor que ultrapasse R$ 1.500,00) é marcado como `Suspeito de Fraude`. Valor exatamente em R$ 1.500,00 é permitido. O limite de quantidade é avaliado antes do limite de valor; se houver violação de qualquer regra (ou de ambas), a solicitação é `Suspeito de Fraude`. Uma solicitação que não cabe não bloqueia uma solicitação menor posterior que caiba.
- **`ValorReembolsado` para todos os status:** sempre calculado (percentual por tipo, teto de R$ 500,00 por procedimento), inclusive para `Rejeitado` e `Suspeito de Fraude`.
- **Dinheiro e arredondamento:** `decimal` para valores monetários, arredondamento para 2 casas com `MidpointRounding.AwayFromZero`.
- **Data de referência injetada:** a data de referência é parâmetro explícito; as regras não leem `DateTime.Now`. A camada externa usa `TimeProvider` (CLI) ou o valor recebido (API).
- **CLI e escrita de CSV:** a CLI escreve diretamente no `TextWriter` de saída (sem buffer intermediário string/MemoryStream desnecessário), preservando saída idêntica (LF, sem BOM).
- **Flutter (UI):** layouts responsivos com rolagem da tela inteira em mobile/tablet (CustomScrollView/slivers) e coluna de resultados rolável no desktop, lista de itens preguiçosa, estados vazio/carregando/erro centralizados, timeout de requisição (30s) com mensagem amigável em português, acessibilidade (rótulos em português, status por texto+ícone, alvos de toque >= 48dp, sem anúncios semânticos duplicados) e `statusFromApi` estrito (status desconhecido => erro).
- **Sem CORS e sem autenticação:** API local de desenvolvimento consumida por app nativo (CORS não é necessário). Não há autenticação.

## O que ficou de fora e por quê

- **Autenticação/autorização:** fora do escopo de uma API local de desafio.
- **Persistência/banco de dados:** processamento em memória por lote, conforme especificado.
- **Paginação:** não exigida para o lote processado.
- **Exportação/compartilhamento de CSV no app:** não implementado na UI. A API oferece saída em CSV (`?format=csv` / `Accept: text/csv`), mas não há ação dedicada de exportação/compartilhamento no aplicativo.
- **Execução end-to-end em emulador/dispositivo Android:** **não validada** nesta entrega (depende de ambiente Android/emulador totalmente provisionado). O comportamento do app foi validado por `flutter analyze` e `flutter test`; o backend foi validado por `dotnet build -warnaserror` e `dotnet test`.

## Como validar a entrega

Executar, em ordem, na raiz do repositório (exceto onde indicado):

```bash
# 1) Backend: build e testes
dotnet build
dotnet test

# 2) CLI: saída idêntica à amostra e tratamento de argumento desconhecido
cat samples/sample.csv | dotnet run --project src/Reimbursement.Cli -- --reference-date 2024-05-01 | diff -u samples/sample-expected.csv -
cat samples/sample.csv | dotnet run --project src/Reimbursement.Cli -- --reference-dat 2024-05-01 ; echo $?

# 3) Flutter (em /mobile)
cd mobile
flutter pub get
flutter analyze
flutter test
cd ..

# 4) Pacote de entrega e verificações de conteúdo
bash scripts/create-zip.sh
unzip -l reimbursement-solution.zip | grep -E "/(bin|obj|build|\.dart_tool)/" | wc -l   # deve imprimir 0
unzip -l reimbursement-solution.zip | tail -1                                              # tamanho bem abaixo de 50 MB
```

## Uso de IA

- **OpenCode (agente de programação com IA):** gerou e aplicou o código nesta iteração e nas anteriores (núcleo, API, CLI, app Flutter, testes e ajustes).
- **Claude:** revisou a solução entregue em relação ao enunciado, executou/definiu casos de borda e escreveu as listas de correções (prompts) que o OpenCode aplicou. Não foram realizadas ações além do que está descrito nesta seção.

As decisões e o código foram alinhados aos requisitos do desafio.
