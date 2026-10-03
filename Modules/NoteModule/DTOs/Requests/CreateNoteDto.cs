namespace smart_pet_care_api.Modules.NoteModule.DTOs.Requests
{
    public class CreateNoteDto
    {
        public string Title { get; set; } = null!;
        public string Content { get; set; } = null!;
    }
}
