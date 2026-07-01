using FacialBiometric.Application.Features.Users.Register;
using FacialBiometric.Domain.Biometrics;
using FacialBiometric.Domain.Entities;
using FacialBiometric.Domain.Primitives;
using FacialBiometric.Domain.Repositories;
using FacialBiometric.Domain.Storage;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace FacialBiometric.Tests.Application.Users;

public sealed class RegisterUserCommandHandlerTests
{
    private readonly IUserRepository _repository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPhotoStorageService _photoStorage = Substitute.For<IPhotoStorageService>();
    private readonly IFacialBiometricProvider _facialBiometricProvider = Substitute.For<IFacialBiometricProvider>();

    private RegisterUserCommandHandler CreateHandler()
        => new(_repository, _unitOfWork, _photoStorage, _facialBiometricProvider);

    [Fact]
    public async Task Handle_ShouldRegisterUser_EvenWhenNoFaceIsDetectedInThePhoto()
    {
        // Arrange — simula uma foto onde o FaceONNX não detectou nenhum rosto
        _facialBiometricProvider
            .ExtractEmbeddingAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<FaceEmbeddingResult>(
                new Error("FacialBiometric.NoFaceDetected", "Nenhum rosto foi detectado na foto enviada.")));

        _photoStorage
            .SaveAsync(Arg.Any<long>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("1/foto.jpg");

        var command = new RegisterUserCommand("Rodrigo Furlaneti", [1, 2, 3], "foto.jpg");
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(2).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenFullNameIsEmpty()
    {
        var command = new RegisterUserCommand(string.Empty, [1, 2, 3], "foto.jpg");
        var handler = CreateHandler();

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.EmptyFullName");
        await _repository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }
}
