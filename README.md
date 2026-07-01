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
| `POST` | `/api/users/authenticate-face` | Autenticação/identificação 1:N — recebe só uma foto (sem `id`) e procura entre todos os usuários cadastrados quem corresponde. Ver seção própria abaixo. |

Todos exigem `[Authorize]` (JWT Bearer). O Swagger (Development) já vem com o
botão **Authorize** configurado — cole o token JWT puro, sem o prefixo
`Bearer ` (o Swashbuckle adiciona automaticamente).

### Cadastro — checagem de rosto duplicado

`RegisterUserCommandHandler` extrai o embedding da foto **antes** de gravar
qualquer coisa no banco e compara contra o de todos os usuários ativos já
cadastrados (`IUserRepository.GetActiveWithFaceEmbeddingAsync`, via
`IFacialBiometricProvider.CompareEmbeddingsAsync` — compara dois embeddings já
extraídos, sem reprocessar imagem). Se a similaridade de algum já cadastrado
passar do `MatchThreshold`, o cadastro é recusado com `409 User.AlreadyExists`
e nada é persistido. Se a foto nova não tiver rosto detectável, a checagem é
pulada e o cadastro segue como antes (embedding fica `NULL`).

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
imagem), não é tratado como "não autenticado".

⚠️ Como o controller inteiro está com `[Authorize]`, hoje essa rota também
exige um JWT válido — ou seja, ela serve para *identificar* um usuário já
autenticado por outro meio, não para logar alguém do zero sem token. Se a
intenção for usá-la como o próprio mecanismo de login (chamada antes de existir
qualquer token), marque a action com `[AllowAnonymous]`.

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

### Erros possíveis

| Código | Quando acontece |
|---|---|
| `FacialBiometric.InvalidImage` | Arquivo enviado não é uma imagem válida/legível |
| `FacialBiometric.NoFaceDetected` | Nenhum rosto identificável na foto |
| `FacialBiometric.InvalidStoredEmbedding` | Embedding salvo no banco está corrompido/formato antigo |
| `User.AlreadyExists` (409) | No cadastro (`POST /api/users`), o rosto enviado já corresponde a outro usuário ativo |

### Performance

`IFacialBiometricProvider` está registrado como **Singleton** (não Scoped) em
`Infrastructure/DependencyInjection.cs`, porque `FaceDetector`,
`Face68LandmarksExtractor` e `FaceEmbedder` carregam sessões do ONNX Runtime —
caro para inicializar a cada requisição. As chamadas `Forward(...)` do
FaceONNX são thread-safe para uso concorrente.

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
