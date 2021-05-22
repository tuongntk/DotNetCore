using System;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DotNetCore.Messaging.RabbitMq.Configurations;
using DotNetCore.Messaging.RabbitMq.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using DotNetCore.Extensions;

namespace DotNetCore.Messaging.RabbitMq
{
    public class RabbitMqSubscriber : IBusSubscriber
    {
        private static readonly ConcurrentDictionary<string, ChannelInfo> Channels = new();
        private readonly IServiceProvider _serviceProvider;
        private readonly IBusPublisher _publisher;
        private readonly ILogger _logger;
        private readonly int _retries;
        private readonly int _retryInterval;
        private readonly RabbitMqOptions _options;
        private readonly IConnection _connection;
        private readonly bool _requeueFailedMessages;

        public RabbitMqSubscriber(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _connection = _serviceProvider.GetRequiredService<IConnection>();
            _publisher = _serviceProvider.GetRequiredService<IBusPublisher>();
            _logger = _serviceProvider.GetRequiredService<ILogger<RabbitMqSubscriber>>();
            _options = _serviceProvider.GetRequiredService<RabbitMqOptions>();
            _retries = _options.Retries >= 0 ? _options.Retries : 3;
            _retryInterval = _options.RetryInterval > 0 ? _options.RetryInterval : 2;
            _requeueFailedMessages = _options.RequeueFailedMessages;

            if (_options.Qos.PrefetchCount < 1)
            {
                _options.Qos.PrefetchCount = 1;
            }

            _connection.CallbackException += ConnectionOnCallbackException;
            _connection.ConnectionShutdown += ConnectionOnConnectionShutdown;
            _connection.ConnectionBlocked += ConnectionOnConnectionBlocked;
            _connection.ConnectionUnblocked += ConnectionOnConnectionUnblocked;
        }

        public IBusSubscriber Subscribe<T>(Func<IServiceProvider, T, Task> handle) where T : class
        {
            var channelKey = $"{_options.Exchange.Name}:{_options.Queue.Name}:{_options.Exchange.RoutingKey}";
            if (Channels.ContainsKey(channelKey))
            {
                return this;
            }

            var channel = _connection.CreateModel();
            if (!Channels.TryAdd(channelKey, new ChannelInfo(channel)))
            {
                channel.Dispose();
                return this;
            }

            _logger.LogTrace($"Created a channel: {channel.ChannelNumber}");

            var declare = _options.Queue?.Declare ?? true;
            var durable = _options.Queue?.Durable ?? true;
            var exclusive = _options.Queue?.Exclusive ?? false;
            var autoDelete = _options.Queue?.AutoDelete ?? false;
            var info = string.Empty;

            info = $" [queue: '{_options.Queue.Name}', routing key: '{_options.Exchange.RoutingKey}', " +
                      $"exchange: '{_options.Exchange.Name}']";

            if (declare)
            {
                _logger.LogInformation($"Declaring a queue: '{_options.Queue.Name}' with routing key: " +
                                           $"'{_options.Exchange.RoutingKey}' for an exchange: '{_options.Exchange.Name}'.");

                channel.QueueDeclare(_options.Queue.Name, durable, exclusive, autoDelete);
            }

            channel.QueueBind(_options.Queue.Name, _options.Exchange.Name, _options.Exchange.RoutingKey);
            channel.BasicQos(_options.Qos.PrefetchSize, _options.Qos.PrefetchCount, _options.Qos.Global);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += async (model, args) =>
            {
                try
                {
                    var messageId = args.BasicProperties.MessageId;
                    var correlationId = args.BasicProperties.CorrelationId;
                    var timestamp = args.BasicProperties.Timestamp.UnixTime;
                    _logger.LogInformation($"Received a message with id: '{messageId}', " +
                                               $"correlation id: '{correlationId}', timestamp: {timestamp}{info}.");

                    var payload = Encoding.UTF8.GetString(args.Body.Span);
                    var message = JsonSerializer.Deserialize<T>(payload);

                    await TryHandleAsync(channel, message, messageId, args, handle);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.Message);
                    channel.BasicReject(args.DeliveryTag, false);
                    await Task.Yield();
                    throw;
                }
            };

            channel.BasicConsume(_options.Queue.Name, false, consumer);

            return this;
        }

        private async Task TryHandleAsync<TMessage>(IModel channel, TMessage message, string messageId, BasicDeliverEventArgs args, Func<IServiceProvider, TMessage, Task> handle)
        {
            var currentRetry = 0;
            var messageName = message.GetType().Name.Underscore();

            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(_retries, i => TimeSpan.FromSeconds(_retryInterval));

            await retryPolicy.ExecuteAsync(async () =>
            {
                try
                {
                    var retryMessage = string.Empty;
                    retryMessage = currentRetry == 0 ? string.Empty : $"Retry: {currentRetry}'.";

                    var preLogMessage = $"Handling a message: '{messageName}' [id: '{messageId}'] {retryMessage}";

                    _logger.LogInformation(preLogMessage);

                    if (_options.MessageProcessingTimeout.HasValue)
                    {
                        var task = handle(_serviceProvider, message);
                        var result = await Task.WhenAny(task, Task.Delay(_options.MessageProcessingTimeout.Value));
                        if (result != task)
                        {
                            throw new RabbitMqMessageProcessingTimeoutException(messageId);
                        }
                    }
                    else
                    {
                        await handle(_serviceProvider, message);
                    }

                    channel.BasicAck(args.DeliveryTag, false);
                    await Task.Yield();

                    var postLogMessage = $"Handled a message: '{messageName}' [id: '{messageId}'] {retryMessage}";
                    _logger.LogInformation(postLogMessage);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.Message);

                    if (ex is RabbitMqMessageProcessingTimeoutException)
                    {
                        channel.BasicNack(args.DeliveryTag, false, _requeueFailedMessages);
                        await Task.Yield();
                        return;
                    }

                    currentRetry++;
                    var errorMessage = $"Unable to handle a message: '{messageName}' [id: '{messageId}'] " +
                                          $"retry {currentRetry - 1}/{_retries}...";

                    if (currentRetry > 1)
                    {
                        _logger.LogError(errorMessage);
                    }

                    if (currentRetry - 1 < _retries)
                    {
                        throw new Exception(errorMessage, ex);
                    }

                    _logger.LogError($"Handling a message: '{messageName}' [id: '{messageId}'] failed.");

                    channel.BasicNack(args.DeliveryTag, false, _requeueFailedMessages);
                    await Task.Yield();
                }
            });
        }

        public void Dispose()
        {
            foreach (var (key, channel) in Channels)
            {
                channel?.Dispose();
                Channels.TryRemove(key, out _);
            }

            _connection.CallbackException -= ConnectionOnCallbackException;
            _connection.ConnectionShutdown -= ConnectionOnConnectionShutdown;
            _connection.ConnectionBlocked -= ConnectionOnConnectionBlocked;
            _connection.ConnectionUnblocked -= ConnectionOnConnectionUnblocked;

            _connection?.Dispose();
        }

        private void ConnectionOnCallbackException(object sender, CallbackExceptionEventArgs eventArgs)
        {
            _logger.LogError("RabbitMQ callback exception occured.");
            if (eventArgs.Exception is not null)
            {
                _logger.LogError(eventArgs.Exception, eventArgs.Exception.Message);
            }

            if (eventArgs.Detail is not null)
            {
                _logger.LogError(JsonSerializer.Serialize(eventArgs.Detail));
            }
        }

        private void ConnectionOnConnectionShutdown(object sender, ShutdownEventArgs eventArgs)
        {
            _logger.LogError($"RabbitMQ connection shutdown occured. Initiator: '{eventArgs.Initiator}', " +
                             $"reply code: '{eventArgs.ReplyCode}', text: '{eventArgs.ReplyText}'.");
        }

        private void ConnectionOnConnectionBlocked(object sender, ConnectionBlockedEventArgs eventArgs)
        {
            _logger.LogError($"RabbitMQ connection has been blocked. {eventArgs.Reason}");
        }

        private void ConnectionOnConnectionUnblocked(object sender, EventArgs eventArgs)
        {
            _logger.LogInformation($"RabbitMQ connection has been unblocked.");
        }

        private class ChannelInfo : IDisposable
        {
            public IModel Channel { get; }

            public ChannelInfo(IModel channel)
            {
                Channel = channel;
            }

            public void Dispose()
            {
                Channel?.Dispose();
            }
        }
    }
}