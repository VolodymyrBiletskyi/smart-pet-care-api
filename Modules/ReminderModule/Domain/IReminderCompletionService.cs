using smart_pet_care_api.Modules.ReminderModule.DTOs.Requests;
using smart_pet_care_api.Modules.ReminderModule.DTOs.Responses;

namespace smart_pet_care_api.Modules.ReminderModule.Domain
{
    public interface IReminderCompletionService
    {
        /// <summary>
        /// Handles the Done button: closes the occurrence, recalculates the next trigger and
        /// files the log where that type belongs. One call, one transaction, so the client
        /// cannot leave a record unlinked or a schedule unmoved.
        /// </summary>
        Task<ReminderCompletionResponseDto> CompleteAsync(Guid reminderId, Guid userId, CompleteReminderDto dto);
    }
}
