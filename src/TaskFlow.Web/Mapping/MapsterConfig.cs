using Mapster;
using TaskFlow.Application.Dtos;
using TaskFlow.Web.ViewModels;

namespace TaskFlow.Web.Mapping;

public static class MapsterConfig
{
    public static void Configure()
    {
        TypeAdapterConfig<CardDto, CardViewModel>.NewConfig();
    }
}
