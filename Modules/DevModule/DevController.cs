using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.AuthModule.Jwt;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.DevModule
{
    [ApiController]
    [Authorize]
    [Route("api/dev")]
    public class DevController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public DevController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        /// <summary>
        /// Creates a test pet for the authenticated user. Dev environment only.
        /// </summary>
        [HttpPost("seed-pet")]
        public async Task<IActionResult> SeedPet()
        {
            if (!_env.IsDevelopment())
                return NotFound();

            var userId = User.GetUserId();

            var existing = await _db.Pets.FirstOrDefaultAsync(p => p.UserId == userId && p.Name == "TestPet");
            if (existing != null)
                return Ok(new { existing.Id, existing.Name, note = "already exists" });

            var pet = new Pet
            {
                UserId = userId,
                Name = "TestPet",
                Species = "Dog",
                Sex = Sex.Male
            };

            _db.Pets.Add(pet);
            await _db.SaveChangesAsync();

            return Ok(new { pet.Id, pet.Name, note = "created" });
        }
    }
}
