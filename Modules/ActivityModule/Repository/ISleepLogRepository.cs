using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.ActivityModule.Repository
{
    public interface ISleepLogRepository
    {
        Task<bool> PetBelongsToUserAsync(Guid petId, Guid userId);
        Task<IReadOnlyList<SleepLog>> GetByPetIdAsync(Guid petId, DateTime? from = null, DateTime? to = null);
        Task<SleepLog?> GetByIdAsync(Guid id);
        Task<SleepLog?> GetTrackedByIdAsync(Guid id);

        /// <summary>
        /// Hours already logged for a day, so the service can hold the 24-hour cap.
        /// <paramref name="excludeSleepLogId"/> leaves a row out of the sum, which an edit
        /// needs: a row must not be weighed against the version of itself still in the table.
        /// </summary>
        Task<decimal> GetLoggedHoursAsync(Guid petId, DateTime sleepDate, Guid? excludeSleepLogId = null);

        Task<SleepLog> AddAsync(SleepLog entity);
        void Delete(SleepLog entity);
        Task<int> SaveChangesAsync();
    }
}
