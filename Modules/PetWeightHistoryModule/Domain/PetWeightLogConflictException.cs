namespace smart_pet_care_api.Modules.PetWeightHistoryModule.Domain
{
    public class PetWeightLogConflictException : Exception
    {
        public PetWeightLogConflictException(string message) : base(message)
        {
        }
    }
}
