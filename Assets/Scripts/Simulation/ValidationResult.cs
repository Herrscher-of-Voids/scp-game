namespace Scp.Simulation
{
    public readonly struct ValidationResult
    {
        public ValidationResult(bool isValid, string error)
        {
            IsValid = isValid;
            Error = error;
        }

        public bool IsValid { get; }

        public string Error { get; }

        public static ValidationResult Success()
        {
            return new ValidationResult(true, string.Empty);
        }

        public static ValidationResult Failure(string error)
        {
            return new ValidationResult(false, error);
        }
    }
}
