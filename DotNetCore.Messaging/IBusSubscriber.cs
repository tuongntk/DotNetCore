using System;
using System.Threading.Tasks;

namespace DotNetCore.Messaging
{
    public interface IBusSubscriber : IDisposable
    {
        IBusSubscriber Subscribe<T>(Func<IServiceProvider, T, Task> handle) where T : class;
    }
}
