using FacialBiometric.Domain.Primitives;
using MediatR;

namespace FacialBiometric.Application.Abstractions.Messaging;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}
