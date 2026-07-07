namespace smart_pet_care_api.Common.Patching
{
    public readonly struct PatchField<T>
    {
        public bool IsSet { get; init; }
        public T? Value { get; init; }

        public static PatchField<T> Set(T? value) => new()
        {
            IsSet = true,
            Value = value
        };
    }
}
