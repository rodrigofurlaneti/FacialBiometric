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
    IFacialBiometricProvider facialBiometricProvider)
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

        var faceEmbedding = embeddingResult.IsSuccess ? embeddingResult.Value.Embedding : null;

        // 3. Checar se o rosto já pertence a alguém cadastrado.
        if (faceEmbedding is not null)
        {
            var existingUsers = await repository.GetActiveWithFaceEmbeddingAsync(cancellationToken);

            foreach (var existingUser in existingUsers)
            {
                var matchResult = await facialBiometricProvider.CompareEmbeddingsAsync(
                    existingUser.FaceEmbedding!, faceEmbedding, cancellationToken);

                if (matchResult.IsSuccess && matchResult.Value.IsMatch)
                {
                    return Result.Failure<long>(DuplicateFaceError);
                }
            }
        }

        // 4. Persistir para obter o Id (necessário para nomear o arquivo da foto)
        await repository.AddAsync(user, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        // 5. Salvar a foto no storage físico
        var photoPath = await photoStorage.SaveAsync(
            user.Id, request.PhotoContent, request.PhotoFileName, cancellationToken);

        // 6. Gravar o resultado do cadastro biométrico no aggregate
        var registerPhotoResult = user.RegisterPhoto(photoPath, faceEmbedding);
        if (registerPhotoResult.IsFailure)
        {
            return Result.Failure<long>(registerPhotoResult.Error);
        }

        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(user.Id);
    }
}
