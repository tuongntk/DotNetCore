using System.Threading.Tasks;

namespace DotNetCore
{
    public interface IInitializer
    {
        Task InitializeAsync();
    }
}
