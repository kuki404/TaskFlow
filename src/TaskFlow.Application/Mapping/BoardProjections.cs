using System.Linq.Expressions;
using TaskFlow.Application.Dtos;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Mapping;

/// <summary>
/// Static Expression&lt;Func&lt;TEntity,TDto&gt;&gt; projections: EF Core translates these directly to
/// SQL SELECT column lists (never "load the entity, then map in memory") wherever they're used
/// with .Select(...) — the whole point of skipping a repository layer.
/// </summary>
public static class BoardProjections
{
    public static Expression<Func<Card, CardDto>> ToCardDto => card => new CardDto(
        card.Id,
        card.CardListId,
        card.Title,
        card.Description,
        card.Priority,
        card.AssignedUserId,
        null, // filled in by CardService after a batched lookup of assignee display names — keeps this projection free of a join to AspNetUsers
        card.DueDateUtc,
        card.Position,
        card.RowVersion);

    public static Expression<Func<Project, ProjectDto>> ToProjectDto => project => new ProjectDto(
        project.Id,
        project.Name,
        project.CreatedAtUtc,
        project.Board!.Id,
        project.Members.Count);
}
