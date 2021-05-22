using DotNetCore.Messaging.RabbitMq.Configurations;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotNetCore.Extensions;
using Microsoft.Extensions.Logging;
using DotNetCore.Messaging.RabbitMq.Internals;
using Microsoft.AspNetCore.Builder;

namespace DotNetCore.Messaging.RabbitMq
{
    public static class DependencyInjection
    {
        private const string SectionName = "rabbitmq";
        private const string RegistryName = "messageBrokers.rabbitmq";

        public static IAppBuilder AddRabbitMq(this IAppBuilder builder, string sectionName = SectionName,
            Action<ConnectionFactory> connectionFactoryConfigurator = null)
        {
            if (string.IsNullOrWhiteSpace(sectionName))
            {
                sectionName = SectionName;
            }

            var options = builder.GetOptions<RabbitMqOptions>(sectionName);
            builder.Services.AddSingleton(options);

            if (!builder.TryRegister(RegistryName))
            {
                return builder;
            }

            if (options.HostNames is null || !options.HostNames.Any())
            {
                throw new ArgumentException("RabbitMQ hostnames are not specified.", nameof(options.HostNames));
            }


            ILogger<IRabbitMqClient> logger;
            using (var serviceProvider = builder.Services.BuildServiceProvider())
            {
                logger = serviceProvider.GetService<ILogger<IRabbitMqClient>>();
            }

            builder.Services.AddSingleton<IRabbitMqClient, RabbitMqClient>();
            builder.Services.AddSingleton<IBusPublisher, RabbitMqPublisher>();
            builder.Services.AddSingleton<IBusSubscriber, RabbitMqSubscriber>();
            builder.Services.AddTransient<RabbitMqExchangeInitializer>();
            builder.Services.AddHostedService<RabbitMqHostedService>();

            builder.AddInitializer<RabbitMqExchangeInitializer>();

            var connectionFactory = new ConnectionFactory
            {
                Port = options.Port,
                VirtualHost = options.VirtualHost,
                UserName = options.Username,
                Password = options.Password,
                RequestedHeartbeat = options.RequestedHeartbeat,
                RequestedConnectionTimeout = options.RequestedConnectionTimeout,
                SocketReadTimeout = options.SocketReadTimeout,
                SocketWriteTimeout = options.SocketWriteTimeout,
                RequestedChannelMax = options.RequestedChannelMax,
                RequestedFrameMax = options.RequestedFrameMax,
                UseBackgroundThreadsForIO = options.UseBackgroundThreadsForIO,
                DispatchConsumersAsync = true,
                ContinuationTimeout = options.ContinuationTimeout,
                HandshakeContinuationTimeout = options.HandshakeContinuationTimeout,
                NetworkRecoveryInterval = options.NetworkRecoveryInterval
            };

            connectionFactoryConfigurator?.Invoke(connectionFactory);

            var connection = connectionFactory.CreateConnection(options.HostNames.ToList(), options.ConnectionName);
            builder.Services.AddSingleton(connection);

            return builder;
        }


        public static IBusSubscriber UseRabbitMq(this IApplicationBuilder app)
            => new RabbitMqSubscriber(app.ApplicationServices);
    }
}