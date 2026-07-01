using FacialBiometric.Domain.Primitives;
using MediatR;

namespace FacialBiometric.Application.Abstractions.Messaging;

public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>
{
}
