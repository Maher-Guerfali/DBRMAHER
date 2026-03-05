namespace BlockSystem.Core
{
    /// <summary>
    /// Default port connection validation logic
    /// </summary>
    public class DefaultPortValidator : IPortValidator
    {
        public bool CanConnect(Port output, Port input)
        {
            // Flow ports can connect to flow ports
            if (output.type == PortType.Flow && input.type == PortType.Flow)
                return true;

            // Flow ports cannot connect to data ports
            if (output.type == PortType.Flow || input.type == PortType.Flow)
                return false;

            // Data ports must match exact types
            return output.type == input.type;
        }

        public string GetValidationError(Port output, Port input)
        {
            if (!CanConnect(output, input))
            {
                return $"Cannot connect {output.type} to {input.type}";
            }
            return null;
        }
    }
}
