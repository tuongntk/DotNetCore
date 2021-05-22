using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotNetCore.Messaging
{
    public interface IBusPublisher
    {
        Task PublishAsync<T>(T message) where T : class;
    }
}
