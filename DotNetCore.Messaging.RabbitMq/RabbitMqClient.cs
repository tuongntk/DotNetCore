using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using DotNetCore.Messaging.RabbitMq.Configurations;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace DotNetCore.Messaging.RabbitMq
{
    public class RabbitMqClient : IRabbitMqClient
    {
        private readonly object _lockObject = new object();
        private readonly IConnection _connection;
        private readonly ILogger<RabbitMqClient> _logger;
        private readonly RabbitMqOptions _options;
        private readonly ConcurrentDictionary<int, IModel> _channels = new ConcurrentDictionary<int, IModel>();
        private readonly int _maxChannels;
        private int _channelsCount;

        public RabbitMqClient(IConnection connection,  RabbitMqOptions options, ILogger<RabbitMqClient> logger)
        {
            _connection = connection;
            _logger = logger;
            _options = options;
            _maxChannels = options.MaxProducerChannels <= 0 ? 1000 : options.MaxProducerChannels;
        }

        public void Send(object message, string messageId = null, string correlationId = null)
        {
            var threadId = Thread.CurrentThread.ManagedThreadId;
            if (!_channels.TryGetValue(threadId, out var channel))
            {
                lock (_lockObject)
                {
                    if (_channelsCount >= _maxChannels)
                    {
                        throw new InvalidOperationException($"Cannot create RabbitMQ producer channel for thread: {threadId} " +
                                                            $"(reached the limit of {_maxChannels} channels). " +
                                                            "Modify `MaxProducerChannels` setting to allow more channels.");
                    }

                    channel = _connection.CreateModel();
                    _channels.TryAdd(threadId, channel);
                    _channelsCount++;

                    _logger.LogTrace($"Created a channel for thread: {threadId}, total channels: {_channelsCount}/{_maxChannels}");
                }
            }
            else
            {
                _logger.LogTrace($"Reused a channel for thread: {threadId}, total channels: {_channelsCount}/{_maxChannels}");
            }

            var payload = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(payload);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = _options.MessagesPersisted;
            properties.MessageId = string.IsNullOrWhiteSpace(messageId) ? Guid.NewGuid().ToString("N") : messageId;
            properties.CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId;

            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            _logger.LogTrace($"Publishing a message with routing key: '{_options.Exchange.RoutingKey}' " +
                                 $"to exchange: '{_options.Exchange.Name}' " +
                                 $"[id: '{properties.MessageId}', correlation id: '{properties.CorrelationId}']");

            channel.BasicPublish(_options.Exchange.Name, _options.Exchange.RoutingKey, properties, body);
        }
    }
}