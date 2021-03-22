using System;

namespace PrivatePond
{
    public class ConfigurationException : Exception
    {
        public ConfigurationException(string code, string message):base(message)
        {
            Source = code;
        }
    }
}