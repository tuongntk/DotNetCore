using System;
using System.Collections.Generic;

namespace DotNetCore.Messaging.RabbitMq
{
    public interface IRabbitMqClient
    {
        void Send(object message, string messageId = null, string correlationId = null);
    }
}
