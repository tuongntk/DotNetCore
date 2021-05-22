using System;

namespace DotNetCore.Messaging.RabbitMq.Exceptions
{
    public class RabbitMqMessageProcessingTimeoutException : Exception
    {
        public string MessageId { get; }

        public RabbitMqMessageProcessingTimeoutException(string messageId)
            : base($"There was a timeout error when handling the message with ID: '{messageId}'")
        {
            MessageId = messageId;
        }
    }
}