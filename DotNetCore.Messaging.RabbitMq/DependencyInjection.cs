using DotNetCore.Messaging.RabbitMq.Configurations;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using DotNetCore.Messaging.RabbitMq.Internals;
using Microsoft.AspNetCore.Builder;
using System.Reflection;

namespace DotNetCore.Messaging.RabbitMq
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddRabbitMq(this IServiceCollection services, RabbitMqOptions rabbitMqOptions)
        {
            if (string.IsNullOrEmpty(rabbitMqOptions.ConnectionString))
            {
                throw new ArgumentException("RabbitMq connection string is not specified.", nameof(rabbitMqOptions.ConnectionString));
            }

            services.AddSingleton(rabbitMqOptions);

            ILogger<IRabbitMqClient> logger;
            using var serviceProvider = services.BuildServiceProvider();
            logger = serviceProvider.GetService<ILogger<IRabbitMqClient>>();

            services.AddSingleton<IRabbitMqClient, RabbitMqClient>();
            services.AddSingleton<IBusPublisher, RabbitMqPublisher>();
            services.AddSingleton<IBusSubscriber, RabbitMqSubscriber>();
            
            services.AddHostedService<RabbitMqHostedService>();

            var connectionFactory = new ConnectionFactory
            {
                Uri = new Uri(rabbitMqOptions.ConnectionString),
                RequestedChannelMax = rabbitMqOptions.RequestedChannelMax,
                DispatchConsumersAsync = true
            };

            var connection = connectionFactory.CreateConnection();
            services.AddSingleton(connection);

            ExchangeDeclare(connection, rabbitMqOptions);

            return services;
        }


        private static void ExchangeDeclare(IConnection connection, RabbitMqOptions rabbitMqOptions)
        {
            var exchanges = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsDefined(typeof(MessageAttribute), false))
                .Select(t => t.GetCustomAttribute<MessageAttribute>().Exchange)
                .Distinct()
                .ToList();

            using var channel = connection.CreateModel();

            if (rabbitMqOptions.Exchange?.Declare == true)
            {
                channel.ExchangeDeclare(rabbitMqOptions.Exchange.Name, rabbitMqOptions.Exchange.Type, rabbitMqOptions.Exchange.Durable, rabbitMqOptions.Exchange.AutoDelete);
            }

            foreach (var exchange in exchanges)
            {
                if (exchange.Equals(rabbitMqOptions.Exchange?.Name, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                channel.ExchangeDeclare(exchange, "topic", true);
            }

            channel.Close();
        }

        public static IBusSubscriber UseRabbitMq(this IApplicationBuilder app) => new RabbitMqSubscriber(app.ApplicationServices);
    }
}