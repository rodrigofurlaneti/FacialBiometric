using FacialBiometric.Application.Abstractions.Messaging;
using FacialBiometric.Domain.Biometrics;
using FacialBiometric.Domain.Entities;
using FacialBiometric.Domain.Primitives;
using FacialBiometric.Domain.Repositories;
using FacialBiometric.Domain.Storage;

namespace FacialBiometric.Application.Features.Users.Register;

internal sealed class RegisterUserCommandHandler(
    IUserRepository repository,
    IUnitOfWork unitOfWork,
    IPhotoStorageService photoStorage,
    IFacialBiometricProvider facialBiometricProvider,
    IFaceEmbeddingIndex faceEmbeddingIndex)
    : ICommandHandler<RegisterUserCommand, long>
{
    private static readonly Error DuplicateFaceError = new(
        "User.AlreadyExists",
        "Este rosto já está cadastrado para outro usuário.");

    public async Task<Result<long>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        // 1. Criar o usuário (regra de negócio no Domain) — ainda sem persistir.
        var userResult = User.Create(request.FullName);
        if (userResult.IsFailure)
        {
            return Result.Failure<long>(userResult.Error);
        }

        var user = userResult.Value;

        // 2. Extrair o embedding facial ANTES de gravar qualquer coisa no banco.
        //    Se nenhum rosto for detectado (ex: foto de baixa qualidade), o cadastro
        //    segue mesmo assim — a foto fica salva e o embedding fica NULL — mas
        //    nesse caso não dá pra checar duplicidade por rosto.
        var embeddingResult = await facialBiometricProvider.ExtractEmbeddingAsync(
            request.PhotoContent, cancellationToken);

        var faceEmbeddingJson = embeddingResult.IsSuccess ? embeddingResult.Value.Embedding : null;
        var faceEmbeddingVector = faceEmbeddingJson is not null
            ? facialBiometricProvider.DecodeEmbedding(faceEmbeddingJson)
            : null;

        // 3. Checar se o rosto já pertence a alguém cadastrado — via índice em memória
        //    (embeddings já decodificados, sem reparsear JSON nem bater no banco de novo).
        if (faceEmbeddingVector is not null)
        {
            var indexedUsers = await faceEmbeddingIndex.GetAllAsync(cancellationToken);

            var duplicate = indexedUsers.FirstOrDefault(entry =>
                facialBiometricProvider.CompareDecodedEmbeddings(entry.Embedding, faceEmbeddingVector).IsMatch);

            if (duplicate is not null)
            {
                return Result.Failure<long>(DuplicateFaceError);
            }
        }

        // 4. Persistir para obter o Id (necessário para nomear o arquivo da foto)
        await repository.AddAsync(user, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        // 5. Salvar a foto no storage físico
        var photoPath = await photoStorage.SaveAsync(
            user.Id, request.PhotoContent, request.PhotoFileName, cancellationToken);

        // 6. Gravar o resultado do cadastro biométrico no aggregate
        var registerPhotoResult = user.RegisterPhoto(photoPath, faceEmbeddingJson);
        if (registerPhotoResult.IsFailure)
        {
            return Result.Failure<long>(registerPhotoResult.Error);
        }

        await unitOfWork.CommitAsync(cancellationToken);

        // 7. Atualiza o índice em memória na hora — sem esperar o próximo recarregamento —
        //    pra que uma tentativa de cadastro duplicado logo em seguida já seja pega.
        if (faceEmbeddingVector is not null)
        {
            faceEmbeddingIndex.Upsert(user.Id, user.FullName, faceEmbeddingVector);
        }

        return Result.Success(user.Id);
    }
}
