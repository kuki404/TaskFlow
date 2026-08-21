using System.ComponentModel.DataAnnotations;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Dtos;

public record BoardDto(Guid Id, Guid ProjectId, string ProjectName, IReadOnlyList<CardListDto> CardLists);

public record CardListDto(Guid Id, string Name, int Position, IReadOnlyList<CardDto> Cards);

public record CardDto(
    Guid Id,
    Guid CardListId,
    string Title,
    string? Description,
    CardPriority Priority,
    Guid? AssignedUserId,
    string? AssignedUserDisplayName,
    DateTime? DueDateUtc,
    int Position,
    byte[] RowVersion);

public record CreateCardListRequest([Required, MaxLength(100)] string Name);

public record MoveCardListRequest([Range(0, int.MaxValue)] int Position);

public record CreateCardRequest(
    [Required] Guid CardListId,
    [Required, MaxLength(200)] string Title,
    [MaxLength(2000)] string? Description,
    CardPriority Priority,
    Guid? AssignedUserId,
    DateTime? DueDateUtc);

public record UpdateCardRequest(
    [Required, MaxLength(200)] string Title,
    [MaxLength(2000)] string? Description,
    CardPriority Priority,
    Guid? AssignedUserId,
    DateTime? DueDateUtc,
    [Required] byte[] RowVersion);

public record MoveCardRequest(
    [Required] Guid TargetCardListId,
    [Range(0, int.MaxValue)] int Position,
    [Required] byte[] RowVersion);

/// <summary>Flat projection of a card assigned to the current user, carrying enough board/project context to render a cross-board "My cards" list without N+1 lookups.</summary>
public record MyCardDto(
    Guid Id,
    string Title,
    string? Description,
    CardPriority Priority,
    DateTime? DueDateUtc,
    Guid BoardId,
    Guid ProjectId,
    string ProjectName,
    string CardListName);
