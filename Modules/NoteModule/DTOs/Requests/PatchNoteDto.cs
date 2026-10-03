using smart_pet_care_api.Common.Patching;

namespace smart_pet_care_api.Modules.NoteModule.DTOs.Requests
{
    public class PatchNoteDto
    {
        // Both fields are required on the row, so `null` is rejected rather than
        // treated as "clear" -- a note with no title or no text is not a note.
        public PatchField<string> Title { get; set; }
        public PatchField<string> Content { get; set; }
    }
}
