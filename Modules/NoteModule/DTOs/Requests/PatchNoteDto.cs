using smart_pet_care_api.Common.Patching;

namespace smart_pet_care_api.Modules.NoteModule.DTOs.Requests
{
    public class PatchNoteDto
    {
        // `null` clears, as everywhere else in the repo. Clearing the last
        // remaining field is what fails, not clearing either one.
        public PatchField<string> Title { get; set; }
        public PatchField<string> Content { get; set; }
    }
}
