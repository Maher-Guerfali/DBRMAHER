namespace BlockSystem.Core
{
    /// <summary>
    /// Interface for validating port connections
    /// Allows custom validation rules per project
    /// </summary>
    public interface IPortValidator
    {
        bool CanConnect(Port output, Port input);
        string GetValidationError(Port output, Port input);
    }
}
