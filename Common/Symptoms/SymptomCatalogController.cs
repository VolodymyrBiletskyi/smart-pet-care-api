using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace smart_pet_care_api.Common.Symptoms
{
    [ApiController]
    [Authorize]
    [Route("api/symptoms")]
    public class SymptomCatalogController : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<SymptomCatalogItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult GetAll() => Ok(SymptomCatalog.Items);

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(SymptomCatalogItemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult GetById(int id)
        {
            var item = SymptomCatalog.GetById(id);
            if (item is null) return NotFound();
            return Ok(item);
        }
    }
}
