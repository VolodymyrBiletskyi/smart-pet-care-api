using Microsoft.AspNetCore.Mvc;

namespace smart_pet_care_api.Modules.PetModule.DTOs;

public class UploadPetPhotoDto
{
    [FromForm(Name = "photo")]
    public IFormFile? Photo { get; set; }
}
