using System.Linq.Expressions;
using TaskFlow.Application.Dtos;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Mapping;

public static class ProjectProjections
{
    /// <summary>Requires the caller's userId to compute MyRole in-query — parameterized via a closure at each call site (see ProjectService).</summary>
    public static Expression<Func<Project, ProjectRole>> RoleFor(Guid userId) =>
        p => p.Members.Where(m => m.UserId == userId).Select(m => m.Role).FirstOrDefault();
}
