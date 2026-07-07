namespace smart_pet_care_api.Modules.PetModule.Domain;

public interface IPetPhotoCleanupService
{
    Task DeletePetPhotosBestEffortAsync(IEnumerable<string?> photoPublicIds);
}
