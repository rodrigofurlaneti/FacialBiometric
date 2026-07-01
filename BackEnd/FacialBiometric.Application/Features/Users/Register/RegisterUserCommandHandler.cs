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
    public async Task<Result<long>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        // 1. Criar o usuário (regra de negócio no Domain)
        var userResult = User.Create(request.FullName);
        if (userResult.IsFailure)
        {
            return Result.Failure<long>(userResult.Error);
        }

        var user = userResult.Value;

        // 2. Persistir para obter o Id (necessário para nomear o arquivo da foto)
        await repository.AddAsync(user, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        // 3. Salvar a foto no storage físico
        var photoPath = await photoStorage.SaveAsync(
            user.Id, request.PhotoContent, request.PhotoFileName, cancellationToken);

        // 4. Extrair o embedding facial (FaceONNX). Se nenhum rosto for detectado
        //    (ex: foto de baixa qualidade), o cadastro segue mesmo assim — a foto
        //    fica salva e o embedding fica NULL, podendo ser reprocessado depois.
        var embeddingResult = await facialBiometricProvider.ExtractEmbeddingAsync(
            request.PhotoContent, cancellationToken);

        var faceEmbedding = embeddingResult.IsSuccess ? embeddingResult.Value.Embedding : null;

        // 5. Gravar o resultado do cadastro biométrico no aggregate
        var registerPhotoResult = user.RegisterPhoto(photoPath, faceEmbedding);
        if (registerPhotoResult.IsFailure)
        {
            return Result.Failure<long>(registerPhotoResult.Error);
        }

        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(user.Id);
    }
}
