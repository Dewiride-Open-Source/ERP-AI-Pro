namespace Dewiride.Erp.BuildingBlocks.Application.Pipeline;

public sealed record HandlerDescriptor(Type HandlerType, Type RequestType, HandlerKind Kind);
