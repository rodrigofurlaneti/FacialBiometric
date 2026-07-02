# FacialBiometricAPI — microsserviço novo

Solution .NET 9 separada e independente, com banco próprio **FacialBiometricDB**
(não referencia o OpenFinancialExchange). Segue os mesmos padrões
arquiteturais do projeto principal (Clean Architecture, CQRS/MediatR,
FluentValidation, EF Core, Result Pattern).

## Estrutura entregue

```
FacialBiometricAPI/
├── Sql/create_facialbiometric_db.sql        ← CREATE DATABASE + tabela Users
└── BackEnd/FacialBiometricAPI/
    ├── FacialBiometricAPI.sln
    ├── FacialBiometricAPI.Domain/           ← zero dependências externas
    │   ├── Primitives/                      ← Entity, AggregateRoot, Result, Error, ValueObject, IDomainEvent
    │   ├── Entities/User.cs
    │   ├── Repositories/IUserRepository.cs, IUnitOfWork.cs
    │   ├── Biometrics/IFacialBiometricProvider.cs (+ resultados)
    │   └── Storage/IPhotoStorageService.cs
    ├── FacialBiometricAPI.Application/      ← MediatR + FluentValidation
    │   ├── Abstractions/Messaging/          ← ICommand, IQuery, handlers
    │   └── Features/Users/Register|HasPhoto|VerifyFace/
    ├── FacialBiometricAPI.Infrastructure/   ← EF Core + repositórios
    │   ├── Persistence/AppDbContext.cs, Configurations/, Repositories/
    │   ├── Storage/FileSystemPhotoStorageService.cs
    │   └── Biometrics/LocalFacialBiometricProvider.cs  ← modelo real (FaceONNX)
    ├── FacialBiometricAPI.API/              ← JWT + Swagger + Controllers
    │   ├── Program.cs, appsettings.json
    │   └── Controllers/UsersController.cs
    └── FacialBiometricAPI.Tests/            ← xUnit + FluentAssertions + NSubstitute
```

Todos os projetos têm `.csproj` prontos e há um `.sln` amarrando tudo —
é só abrir e restaurar (`dotnet restore`).

## Banco de dados

`Sql/create_facialbiometric_db.sql` cria o banco `FacialBiometricDB` (se não
existir) e a tabela `Users`, **totalmente independente** do
`OpenFinancialExchange.Users` (sem FK entre bancos, conforme decidido):

```sql
Users
  Id                 BIGINT IDENTITY PK
  FullName           NVARCHAR(200) NOT NULL
  PhotoPath          NVARCHAR(500) NULL
  FaceEmbedding      NVARCHAR(MAX) NULL
  PhotoRegisteredAt  DATETIME2 NULL
  IsActive           BIT
  CreatedAt/UpdatedAt DATETIME2
```

## Endpoints

| Método | Rota | Descrição |
|---|---|---|
| `POST` | `/api/users` | Cadastra usuário (FullName + Photo, multipart/form-data). Rejeita com `409 User.AlreadyExists` se o rosto já pertencer a outro usuário ativo. |
| `GET` | `/api/users/{id}/has-photo` | Retorna `{ hasPhoto: bool, photoRegisteredAt }` |
| `POST` | `/api/users/{id}/verify-face` | Verificação 1:1 — compara a foto enviada com a cadastrada de um usuário já conhecido (`id` na rota) |
| `POST` | `/api/users/authenticate-face` | Autenticação/identificação 1:N — recebe só uma foto (sem `id`) e procura entre todos os usuários cadastrados quem corresponde. Passa por checagem de liveness antes de comparar. Ver seção própria abaixo. |
| `POST` | `/api/calibration/threshold` | Ferramenta de apoio — recebe fotos rotuladas e sugere um threshold com base em FAR/FRR. Ver seção própria abaixo. |

Todos exigem `[Authorize]` (JWT Bearer). O Swagger (Development) já vem com o
botão **Authorize** configurado — cole o token JWT puro, sem o prefixo
`Bearer ` (o Swashbuckle adiciona automaticamente).

### Cadastro — checagem de rosto duplicado

`RegisterUserCommandHandler` extrai o embedding da foto **antes** de gravar
qualquer coisa no banco e compara contra o de todos os usuários ativos já
cadastrados (via `IFaceEmbeddingIndex` — ver seção de performance abaixo). Se a
similaridade de algum já cadastrado passar do `MatchThreshold`, o cadastro é
recusado com `409 User.AlreadyExists` e nada é persistido. Se a foto nova não
tiver rosto detectável, a checagem é pulada e o cadastro segue como antes
(embedding fica `NULL`).

### Autenticação por Face ID (1:N) — `POST /api/users/authenticate-face`

Pensado como "login por rosto": recebe uma única foto (`Photo`,
multipart/form-data) e devolve:

```jsonc
// rosto reconhecido
{
  "isAuthenticated": true,
  "userId": 1,
  "fullName": "Rodrigo Luiz Madeira Furlaneti",
  "confidence": 0.91,
  "message": null
}

// rosto não encontrado na base
{
  "isAuthenticated": false,
  "userId": null,
  "fullName": null,
  "confidence": null,
  "message": "Usuário não existe na base de dados."
}
```

Foto inválida ou sem rosto detectável continua retornando `400` (erro de
imagem), não é tratado como "não autenticado". Uma foto que falhe na checagem
de liveness (ver seção abaixo) retorna `400 FacialBiometric.LivenessCheckFailed`
— também antes de tentar identificar quem é a pessoa.

⚠️ Como o controller inteiro está com `[Authorize]`, hoje essa rota também
exige um JWT válido — ou seja, ela serve para *identificar* um usuário já
autenticado por outro meio, não para logar alguém do zero sem token. Se a
intenção for usá-la como o próprio mecanismo de login (chamada antes de existir
qualquer token), marque a action com `[AllowAnonymous]`.

### Detecção de vivacidade (liveness) — heurística passiva

`AuthenticateFaceCommandHandler` roda uma checagem de liveness (`ILivenessDetector`
→ `PassiveLivenessDetector`, em Infrastructure/Biometrics) antes de comparar a
foto contra a base. **Só nessa rota** — não no cadastro — porque é o "portão de
acesso" de verdade; bloquear o cadastro por uma heurística imprecisa geraria
atrito desnecessário no onboarding.

⚠️ **Isto não é anti-spoofing de produção.** É uma heurística sobre uma única
imagem estática, sem modelo dedicado nem múltiplos frames, com três sinais:

1. **Nitidez** (variância do Laplaciano) — fotos recapturadas de tela/impressão
   tendem a perder detalhe fino.
2. **Estouro de luz/reflexo** (% de pixels quase-brancos) — comum em fotos de tela.
3. **Variedade de cor** (paleta quantizada) — recompressão por tela às vezes
   reduz a variedade efetiva de cor.

O score é a fração dos 3 sinais que "passaram"; `LiveScoreThreshold = 0.5` em
`PassiveLivenessDetector`. Espera-se falso positivo (spoof que passa) e falso
negativo (foto real rejeitada) com alguma frequência — os limiares são pontos
de partida, não calibrados. Antes de confiar nisso pra controle de acesso real:

- Teste com tentativas de spoofing reais (foto impressa, foto de tela, vídeo)
  do seu cenário específico e meça a taxa de detecção.
- Considere evoluir para um desafio de múltiplos frames (piscar, virar a
  cabeça) ou um modelo de anti-spoofing dedicado — a interface `ILivenessDetector`
  foi desenhada pra isso: trocar `PassiveLivenessDetector` por outra implementação
  não exige mudar nada em `AuthenticateFaceCommandHandler`.

## Modelo de biometria facial — implementado

`LocalFacialBiometricProvider` (Infrastructure/Biometrics) agora usa
**[FaceONNX](https://github.com/FaceONNX/FaceONNX)** (detecção YOLOv5-face +
landmarks 68 pontos + embedding ResNet27 512-d, tudo via ONNX Runtime,
100% local — sem chamada a nenhuma API externa). Os modelos `.onnx` vêm
embutidos no próprio pacote NuGet `FaceONNX`, não precisa baixar nada à parte.

Fluxo de `ExtractEmbeddingAsync` / `CompareAsync`:

1. Decodifica a foto (`SixLabors.ImageSharp`) e converte pra `float[3][,]` em BGR.
2. `FaceDetector` detecta o(s) rosto(s) e pega o de maior `Score`.
3. `Face68LandmarksExtractor` extrai os 68 pontos faciais → calcula o ângulo de rotação.
4. `FaceProcessingExtensions.Align` alinha o rosto (corrige inclinação da cabeça).
5. `FaceEmbedder` gera o embedding (vetor de 512 floats), serializado em JSON e
   salvo em `Users.FaceEmbedding`.
6. Na verificação, compara os dois embeddings por **similaridade de cosseno**.

### ⚠️ Threshold — precisa calibrar antes de produção

`MatchThreshold = 0.62f` (em `LocalFacialBiometricProvider`) é um ponto de
partida razoável para embeddings do FaceONNX, **não um valor validado para o
seu caso de uso**. Antes de ir pra produção:

- Rode a verificação com fotos reais do seu público (mesma pessoa em condições
  diferentes de luz/ângulo, e pessoas diferentes) e observe a distribuição de
  similaridade.
- Suba o threshold reduz falsos positivos (aceitar pessoa errada) mas aumenta
  falsos negativos (rejeitar a pessoa certa) — e vice-versa. Essa calibração é
  uma decisão de produto/compliance, não só técnica.
- Considere logar o `Confidence` retornado em `FaceVerificationResponse` por um
  tempo antes de tomar decisões automáticas só com base no `IsMatch`.
- Use a ferramenta `POST /api/calibration/threshold` (seção mais abaixo) como
  ponto de partida quantitativo — ela não substitui testar com dados reais do
  seu público, mas já calcula FAR/FRR reais em vez de "achismo".

### Erros possíveis

| Código | Quando acontece |
|---|---|
| `FacialBiometric.InvalidImage` | Arquivo enviado não é uma imagem válida/legível |
| `FacialBiometric.NoFaceDetected` | Nenhum rosto identificável na foto |
| `FacialBiometric.InvalidStoredEmbedding` | Embedding salvo no banco está corrompido/formato antigo |
| `FacialBiometric.LivenessCheckFailed` (400) | `authenticate-face`: a foto não passou na checagem de liveness |
| `User.AlreadyExists` (409) | No cadastro (`POST /api/users`), o rosto enviado já corresponde a outro usuário ativo |
| `Calibration.NotEnoughPeople` / `NotEnoughValidPhotos` / `NoGenuinePairs` | `POST /api/calibration/threshold`: amostra insuficiente pra calibrar |

### Performance — índice em memória + comparação paralela (1:N)

Tanto o cadastro (checagem de duplicidade) quanto `authenticate-face` precisam
comparar uma foto contra **todos** os usuários cadastrados. Pra não pagar o
custo de reparsear o JSON do embedding a cada comparação (e a cada requisição),
existe `IFaceEmbeddingIndex` (Domain) / `InMemoryFaceEmbeddingIndex`
(Infrastructure, Singleton):

- Mantém em memória o embedding já decodificado (`float[]`) de cada usuário
  ativo, carregado do banco sob demanda na primeira chamada.
- É atualizado na hora (`Upsert`) logo após um novo cadastro — não espera um
  recarregamento completo pra pegar o usuário recém-criado.
- `AuthenticateFaceCommandHandler` compara contra o índice em **paralelo**
  (`Parallel.ForEach`), já que a comparação é só matemática (cosseno) sem I/O.

Duas ressalvas importantes: (1) isso é uma otimização de comparação em memória
(brute-force paralelizado), **não** um índice aproximado tipo HNSW/FAISS — pra
bases muito grandes (dezenas de milhares de usuários+), um índice vetorial
aproximado ainda seria o próximo passo. (2) Num deploy com múltiplas instâncias
da API, cada instância mantém seu próprio índice em memória — um cadastro feito
numa instância só aparece nas outras depois que elas recarregarem
(`IFaceEmbeddingIndex.Invalidate()` + próxima leitura). Ok pra uma instância
única; revisar se escalar horizontalmente.

`IFacialBiometricProvider` continua registrado como **Singleton** (não Scoped)
em `Infrastructure/DependencyInjection.cs`, porque `FaceDetector`,
`Face68LandmarksExtractor` e `FaceEmbedder` carregam sessões do ONNX Runtime —
caro para inicializar a cada requisição. As chamadas `Forward(...)` do
FaceONNX são thread-safe para uso concorrente.

### Criptografia do embedding em repouso

`Users.FaceEmbedding` é dado biométrico — dado sensível pra LGPD/GDPR. A coluna
é criptografada de forma transparente via `EncryptedStringConverter`
(`Infrastructure/Persistence/Converters`), um `ValueConverter` do EF Core que usa
a **Data Protection API** do ASP.NET Core (`IDataProtector`, configurada em
`Infrastructure/DependencyInjection.cs` com `AddDataProtection()` +
`PersistKeysToFileSystem`). Domain e Application continuam lendo/escrevendo o
JSON em texto claro — só o que vai pro banco é o ciphertext.

Configuração:

```json
"DataProtection": {
  "KeysPath": "App_Data/DataProtection-Keys"
}
```

⚠️ Pontos de atenção:

- **Não precisa de nova migration** — a coluna continua `nvarchar(max)` tanto
  antes quanto depois (o converter só muda o valor em runtime, não o tipo).
- **Dado gravado antes desta mudança** (ex: o `FaceEmbedding` de teste já
  cadastrado neste projeto) ficará em texto claro no banco, mas o sistema vai
  tentar descriptografá-lo como se fosse ciphertext e falhar — o
  `EncryptedStringConverter` trata isso como corrompido (retorna `null`), então
  o usuário afetado passa a contar como "sem foto cadastrada" e precisa
  refazer o cadastro.
- **Backup da pasta de chaves é obrigatório.** Perder `App_Data/DataProtection-Keys`
  torna todos os embeddings já gravados irrecuperáveis.
- Em produção com múltiplas instâncias da API, troque `PersistKeysToFileSystem`
  por uma persistência compartilhada (`PersistKeysToDbContext`, blob storage
  etc.) e considere `ProtectKeysWithCertificate`.

### Ferramenta de calibração de threshold — `POST /api/calibration/threshold`

Não substitui testar com uma amostra real do seu público, mas dá um ponto de
partida embasado em vez de "achismo". Contrato (`multipart/form-data`):

- `Photos`: lista de arquivos.
- `PersonLabels`: lista de rótulos (nomes), na mesma ordem — `Photos[i]`
  pertence a `PersonLabels[i]`. Fotos com o mesmo rótulo formam pares
  "genuínos" (mesma pessoa); rótulos diferentes formam pares "impostores".

Precisa de pelo menos 2 pessoas distintas (pra gerar pares impostores) e pelo
menos uma pessoa com 2+ fotos (pra gerar pares genuínos). A ferramenta extrai o
embedding de cada foto, gera todos os pares possíveis, calcula a similaridade
de cada par e testa uma faixa de thresholds (0.30 a 0.95, passo 0.01),
reportando para cada um:

- **FAR** (False Accept Rate) — fração de pares impostores que ficariam acima
  do threshold (aceitos por engano como a mesma pessoa).
- **FRR** (False Reject Rate) — fração de pares genuínos que ficariam abaixo
  do threshold (rejeitados por engano).

Sugere o threshold onde `|FAR − FRR|` é mínimo (aproximação do Equal Error
Rate) e devolve a curva completa, pra você escolher outro ponto se seu caso de
uso pedir priorizar FAR baixo (controle de acesso mais restritivo) ou FRR baixo
(menos atrito, mais tolerante). O resultado só é confiável na medida em que a
amostra enviada parecer com o público/condições reais de uso.

## ⚠️ Um ponto pendente de decisão sua

**Autenticação**: como este é um microsserviço novo e separado, deixei o
`Program.cs` configurado para **validar** (não emitir) tokens JWT usando o
mesmo `Secret`/`Issuer`/`Audience` do OpenFinancialExchange — um padrão comum
em arquitetura de microsserviços (login centralizado, cada serviço só valida).
Se isso não for o que você quer (ex: este serviço deveria ter login próprio,
ou ser público/sem auth), me avise que eu ajusto.

## Setup rápido

1. `cd BackEnd && dotnet restore`
2. Ajustar `FacialBiometric.API/appsettings.json`: `ConnectionStrings:DefaultConnection`
   e `Jwt:Secret` (mesmo valor do OpenFinancialExchange).
3. Criar o banco/schema via EF Core (não existe migration versionada em SQL puro):
   ```
   dotnet tool install --global dotnet-ef   # se ainda não tiver
   dotnet ef migrations add InitialCreate --project FacialBiometric.Infrastructure --startup-project FacialBiometric.API
   dotnet ef database update --project FacialBiometric.Infrastructure --startup-project FacialBiometric.API
   ```
   (o usuário Windows rodando o comando precisa ter permissão `dbcreator`/`sysadmin`
   na instância SQL Server local para o `database update` criar o banco).
4. `dotnet run --project FacialBiometric.API`
5. Abrir `https://localhost:<porta>/swagger`, clicar em **Authorize** e colar um
   JWT válido (mesmo Secret/Issuer/Audience do `appsettings.json`) para testar
   os endpoints — todos exigem `[Authorize]`.
6. Calibrar `MatchThreshold` em `LocalFacialBiometricProvider` com fotos reais
   antes de liberar `/verify-face` e `/authenticate-face` em produção (ver seção acima).
