using AutoFixture;
using DirectoryService.Application.Commands.Departments.Create;

namespace DirectoryService.IntegrationTests;

public class FixtureExtensions
{
    //public static CreateDepartmentCommand SeedInvalidAddPetCommand(this IFixture fixture, Guid volunteerId)
    //{
    //    return fixture.Build<CreateDepartmentCommand>()
    //        .With(c => c.VolunteerId, volunteerId)
    //        .With(c => c.PetName, "")
    //        .With(c => c.Description, "Пёс хорош")
    //        .With(c => c.HealthInfo, "Здоров")
    //        .With(c => c.Address, new AddressDto
    //        (
    //            City: "Moscow",
    //            Street: "Main Street",
    //            House: 10,
    //            Flat: 25
    //        ))
    //        .With(c => c.Vaccinated, true)
    //        .With(c => c.Height, 45)
    //        .With(c => c.Weight, 20)
    //        .With(c => c.SpeciesId, Guid.NewGuid()) //с моим моком будет работать любой Гуид
    //        .With(c => c.BreedId, Guid.NewGuid())
    //        .With(c => c.PetStatus, "LookingHome")
    //        .With(c => c.Color, "коричневый")
    //        .Create();
    //}
}
