using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotNetCore.Messaging.RabbitMq
{
    public class RabbitMqPublisher : IBusPublisher
    {
        private readonly IRabbitMqClient _client;

        public RabbitMqPublisher(IRabbitMqClient client)
        {
            _client = client;
        }

        public Task PublishAsync<T>(T message)
            where T : class
        {
            _client.Send(message);

            return Task.CompletedTask;
        }
    }
}