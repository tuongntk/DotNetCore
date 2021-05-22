using System;
using System.Collections.Generic;

namespace DotNetCore.Messaging.RabbitMq.Configurations
{
    public class RabbitMqOptions
    {
        public string ConnectionString { get; set; }
        public ushort RequestedChannelMax { get; set; }
        public string ConventionsCasing { get; set; }
        public int Retries { get; set; }
        public int RetryInterval { get; set; }
        public bool MessagesPersisted { get; set; }
        public ExchangeOptions Exchange { get; set; }
        public QueueOptions Queue { get; set; }
        public QosOptions Qos { get; set; }
        public int MaxProducerChannels { get; set; }
        public bool RequeueFailedMessages { get; set; }

        public class ExchangeOptions
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public bool Declare { get; set; }
            public bool Durable { get; set; }
            public bool AutoDelete { get; set; }
            public string RoutingKey { get; set; }
        }

        public class QueueOptions
        {
            public string Name { get; set; }
            public bool Declare { get; set; }
            public bool Durable { get; set; }
            public bool Exclusive { get; set; }
            public bool AutoDelete { get; set; }
        }

        public class QosOptions
        {
            public uint PrefetchSize { get; set; }
            public ushort PrefetchCount { get; set; }
            public bool Global { get; set; }
        }
    }
}